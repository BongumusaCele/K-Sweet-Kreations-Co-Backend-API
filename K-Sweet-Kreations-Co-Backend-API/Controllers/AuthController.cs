
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;


namespace K_Sweet_Kreations_Co_Backend_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<UserAccount> _passwordHasher;

        private readonly IConfiguration _configuration;

        public AuthController(
            ApplicationDbContext context,
            IPasswordHasher<UserAccount> passwordHasher,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        // POST: api/Auth/register
        [HttpPost("register"), EnableRateLimiting("identity")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var emailExists = await _context.UserAccounts
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                return Conflict(new
                {
                    message = "An account with this email already exists."
                });
            }

            var user = new UserAccount
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                PhoneNumber = dto.PhoneNumber.Trim(),
                Role = "Customer",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(user, dto.Password);

            _context.UserAccounts.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    message = "Unable to register this account."
                });
            }

            return StatusCode(201, new
            {
                message = "Customer registered successfully.",
                userId = user.UserId,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role
            });
        }


        [HttpPost("login"), EnableRateLimiting("identity")]
        public async Task<IActionResult> Login(
            [FromBody] LoginDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !user.Active)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password);

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(
                    user, dto.Password);

                await _context.SaveChangesAsync();
            }

            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT secret key is missing.");

            var issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException(
                    "JWT issuer is missing.");

            var audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException(
                    "JWT audience is missing.");

            var expiryMinutes = _configuration.GetValue<int>(
                "Jwt:ExpiryMinutes");

            if (expiryMinutes <= 0)
            {
                throw new InvalidOperationException(
                    "JWT expiry must be greater than zero.");
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub,
            user.UserId.ToString()),

        new Claim(JwtRegisteredClaimNames.Email,
            user.Email),

        new Claim(ClaimTypes.NameIdentifier,
            user.UserId.ToString()),

        new Claim(ClaimTypes.Name,
            user.FullName),

        new Claim(ClaimTypes.Role,
            user.Role),

        new Claim(JwtRegisteredClaimNames.Jti,
            Guid.NewGuid().ToString())
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            _context.UserSessions.Add(new UserSession {
                UserId = user.UserId,
                RefreshTokenHash = HashSession(claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value),
                ExpiresAt = expiresAt
            });
            await _context.SaveChangesAsync();

            var tokenString = new JwtSecurityTokenHandler()
                .WriteToken(token);

            Response.Cookies.Append("ksk_session", tokenString, new CookieOptions {
                HttpOnly = true, Secure = SecureCookie, SameSite = SameSiteMode.Strict,
                Expires = expiresAt, Path = "/api"
            });
            return Ok(new
            {
                message = "Login successful.",

                expiresAt = expiresAt,
                userId = user.UserId,
                fullName = user.FullName,
                phoneNumber = user.PhoneNumber,
                email = user.Email,
                role = user.Role
            });
        }

        // POST: api/Auth/create-consultant
        [HttpPost("staff")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateConsultant(
            [FromBody] CreateConsultantDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var emailExists = await _context.UserAccounts
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                return Conflict(new
                {
                    message = "An account with this email already exists."
                });
            }

            var consultant = new UserAccount
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                PhoneNumber = dto.PhoneNumber.Trim(),
                Role = "Staff",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            consultant.PasswordHash =
                _passwordHasher.HashPassword(
                    consultant, dto.Password);

            _context.UserAccounts.Add(consultant);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    message = "Unable to create consultant account."
                });
            }

            return StatusCode(201, new
            {
                message = "Staff account created successfully.",
                userId = consultant.UserId,
                fullName = consultant.FullName,
                email = consultant.Email,
                role = consultant.Role
            });
        }

        private bool SecureCookie => Request.IsHttps || !(HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
            || HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"));
        public static string HashSession(string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));

        [HttpGet("me"), Authorize]
        public async Task<IActionResult> Me()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.UserAccounts.AsNoTracking().SingleAsync(u => u.UserId == id);
            return Ok(new { userId = user.UserId, fullName = user.FullName, email = user.Email, phoneNumber = user.PhoneNumber, role = user.Role });
        }

        [HttpPut("me"), Authorize]
        public async Task<IActionResult> Profile(ContactDetails dto)
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.UserAccounts.SingleAsync(u => u.UserId == id);
            user.FullName = dto.Name.Trim(); user.PhoneNumber = dto.Phone.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { userId = user.UserId, fullName = user.FullName, email = user.Email, phoneNumber = user.PhoneNumber, role = user.Role });
        }

        [HttpPost("logout"), Authorize]
        public async Task<IActionResult> Logout()
        {
            var hash = HashSession(User.FindFirstValue(JwtRegisteredClaimNames.Jti)!);
            var session = await _context.UserSessions.SingleOrDefaultAsync(s => s.RefreshTokenHash == hash);
            if (session != null) { session.RevokedAt = DateTime.UtcNow; await _context.SaveChangesAsync(); }
            Response.Cookies.Delete("ksk_session", new CookieOptions { Path = "/api", Secure = SecureCookie, SameSite = SameSiteMode.Strict });
            return NoContent();
        }

    }
}


using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [NonController]
    [Authorize]
    public class CustomCakeRequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CustomCakeRequestsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/CustomCakeRequests
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateCustomCakeRequest(
            [FromBody] CreateCustomCakeRequestDto dto)
        {
            if (dto.CollectionDateTime <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message = "Collection date must be in the future."
                });
            }

            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int customerId))
            {
                return Unauthorized(new
                {
                    message = "Invalid customer authentication."
                });
            }

            var customerExists = await _context.UserAccounts
                .AnyAsync(u => u.UserId == customerId
                    && u.Active);

            if (!customerExists)
            {
                return Unauthorized(new
                {
                    message = "Customer account not found or inactive."
                });
            }

            var request = new CustomCakeRequest
            {
                CustomerId = customerId,
                CakeSize = dto.CakeSize.Trim(),
                Flavour = dto.Flavour.Trim(),
                Filling = dto.Filling?.Trim(),
                CustomizationDetails =
                    dto.CustomizationDetails?.Trim(),
                CollectionDateTime = dto.CollectionDateTime,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.CustomCakeRequests.Add(request);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Custom cake request submitted successfully.",
                customCakeRequestId = request.CustomCakeRequestId,
                customerId = request.CustomerId,
                cakeSize = request.CakeSize,
                flavour = request.Flavour,
                filling = request.Filling,
                customizationDetails = request.CustomizationDetails,
                collectionDateTime = request.CollectionDateTime,
                status = request.Status,
                createdAt = request.CreatedAt
            });
        }


        // GET: api/CustomCakeRequests/my-requests
        [HttpGet("my-requests")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int customerId))
            {
                return Unauthorized(new
                {
                    message = "Invalid customer authentication."
                });
            }

            var requests = await _context.CustomCakeRequests
                .AsNoTracking()
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.CustomCakeRequestId,
                    r.CakeSize,
                    r.Flavour,
                    r.Filling,
                    r.CustomizationDetails,
                    r.CollectionDateTime,
                    r.Status,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(requests);
        }


        // GET: api/CustomCakeRequests/1
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Customer,Admin,Consultant")]
        public async Task<IActionResult> GetCustomCakeRequest(int id)
        {
            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user authentication."
                });
            }

            var isStaff = User.IsInRole("Admin")
                || User.IsInRole("Consultant");

            var request = await _context.CustomCakeRequests
                .AsNoTracking()
                .Where(r => r.CustomCakeRequestId == id)
                .Where(r => isStaff || r.CustomerId == userId)
                .Select(r => new
                {
                    r.CustomCakeRequestId,
                    r.CustomerId,
                    r.ConsultantId,
                    r.CakeSize,
                    r.Flavour,
                    r.Filling,
                    r.CustomizationDetails,
                    r.CollectionDateTime,
                    r.Status,
                    r.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Custom cake request not found."
                });
            }

            return Ok(request);
        }


        // GET: api/CustomCakeRequests
        [HttpGet]
        [Authorize(Roles = "Admin,Consultant")]
        public async Task<IActionResult> GetAllCustomCakeRequests()
        {
            var requests = await _context.CustomCakeRequests
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.CustomCakeRequestId,
                    r.CustomerId,
                    CustomerName = r.Customer.FullName,
                    r.ConsultantId,
                    r.CakeSize,
                    r.Flavour,
                    r.Filling,
                    r.CustomizationDetails,
                    r.CollectionDateTime,
                    r.Status,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(requests);
        }

        // PATCH: api/CustomCakeRequests/1/status
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "Admin,Consultant")]
        public async Task<IActionResult> UpdateCustomCakeStatus(
            int id,
            [FromBody] UpdateCustomCakeStatusDto dto)
        {
            var request = await _context.CustomCakeRequests
                .FirstOrDefaultAsync(r => r.CustomCakeRequestId == id);

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Custom cake request not found."
                });
            }

            var newStatus = dto.Status.Trim();

            var allowedTransitions = new Dictionary<string, string[]>
            {
                ["Pending"] = new[] { "Under Review", "Rejected" },
                ["Under Review"] = new[] { "Quoted", "Rejected" },
                ["Quoted"] = Array.Empty<string>(),
                ["Rejected"] = Array.Empty<string>()
            };

            if (!allowedTransitions.TryGetValue(
                    request.Status, out var allowedStatuses))
            {
                return Conflict(new
                {
                    message = "The request has an unsupported current status."
                });
            }

            if (!allowedStatuses.Contains(newStatus))
            {
                return BadRequest(new
                {
                    message = $"Cannot change status from " +
                              $"{request.Status} to {newStatus}."
                });
            }

            request.Status = newStatus;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Custom cake request status updated successfully.",
                customCakeRequestId = request.CustomCakeRequestId,
                status = request.Status
            });
        }

        // PATCH: api/CustomCakeRequests/1/assign-consultant
        [HttpPatch("{id:int}/assign-consultant")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignConsultant(
            int id,
            [FromBody] AssignConsultantDto dto)
        {
            // Find the custom cake request
            var request = await _context.CustomCakeRequests
                .FirstOrDefaultAsync(r => r.CustomCakeRequestId == id);

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Custom cake request not found."
                });
            }

            // Do not assign consultants to closed requests
            if (request.Status == "Rejected" ||
                request.Status == "Quoted")
            {
                return BadRequest(new
                {
                    message = "Cannot assign a consultant to a closed request."
                });
            }

            // Verify that the consultant exists and is active
            var consultant = await _context.UserAccounts
                .FirstOrDefaultAsync(u =>
                    u.UserId == dto.ConsultantId &&
                    u.Role == "Consultant" &&
                    u.Active);

            if (consultant == null)
            {
                return BadRequest(new
                {
                    message = "Consultant not found or inactive."
                });
            }

            // Prevent accidental reassignment
            if (request.ConsultantId.HasValue)
            {
                return Conflict(new
                {
                    message = "This request already has an assigned consultant."
                });
            }

            // Assign the consultant
            request.ConsultantId = consultant.UserId;

            // Move pending requests into review
            if (request.Status == "Pending")
            {
                request.Status = "Under Review";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Consultant assigned successfully.",
                customCakeRequestId = request.CustomCakeRequestId,
                consultantId = consultant.UserId,
                consultantName = consultant.FullName,
                status = request.Status
            });
        }



    }
}

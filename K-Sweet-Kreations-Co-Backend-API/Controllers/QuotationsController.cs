
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
    [Authorize]
    public class QuotationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuotationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Quotations
        [HttpPost]
        [Authorize(Roles = "Admin,Consultant")]
        public async Task<IActionResult> CreateQuotation(
            [FromBody] CreateQuotationDto dto)
        {
            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int staffId))
            {
                return Unauthorized(new
                {
                    message = "Invalid staff authentication."
                });
            }

            var staff = await _context.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.UserId == staffId &&
                    u.Active &&
                    (u.Role == "Admin" ||
                     u.Role == "Consultant"));

            if (staff == null)
            {
                return Forbid();
            }

            var request = await _context.CustomCakeRequests
                .FirstOrDefaultAsync(r =>
                    r.CustomCakeRequestId ==
                    dto.CustomCakeRequestId);

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Custom cake request not found."
                });
            }

            // Consultants may only quote requests assigned to them.
            // Admins may quote any request under review.
            if (staff.Role == "Consultant" &&
                request.ConsultantId != staffId)
            {
                return StatusCode(403, new
                {
                    message =
                        "This custom cake request is not assigned to you."
                });
            }

            if (request.Status != "Under Review")
            {
                return BadRequest(new
                {
                    message =
                        "Only requests under review can receive quotations.",
                    currentStatus = request.Status
                });
            }

            var existingQuotation = await _context.Quotations
                .AnyAsync(q =>
                    q.CustomCakeRequestId ==
                        dto.CustomCakeRequestId &&
                    q.Status == "Pending");

            if (existingQuotation)
            {
                return Conflict(new
                {
                    message =
                        "A pending quotation already exists for this request."
                });
            }

            if (dto.QuotedAmount <= 0)
            {
                return BadRequest(new
                {
                    message =
                        "Quoted amount must be greater than zero."
                });
            }

            if (dto.ExpiresAt.HasValue &&
                dto.ExpiresAt.Value <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message =
                        "Quotation expiry must be in the future."
                });
            }

            // Keep the quotation associated with the
            // assigned consultant where one exists.
            // Otherwise, an Admin may act as the quoting staff member.
            var quotationConsultantId =
                request.ConsultantId ?? staffId;

            var quotation = new Quotation
            {
                CustomCakeRequestId =
                    dto.CustomCakeRequestId,

                ConsultantId = quotationConsultantId,

                QuotedAmount = dto.QuotedAmount,

                Notes = dto.Notes?.Trim(),

                Status = "Pending",

                CreatedAt = DateTime.UtcNow,

                ExpiresAt = dto.ExpiresAt
            };

            _context.Quotations.Add(quotation);

            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Quotation created successfully.",
                quotationId = quotation.QuotationId,
                customCakeRequestId =
                    quotation.CustomCakeRequestId,
                consultantId = quotation.ConsultantId,
                quotedAmount = quotation.QuotedAmount,
                notes = quotation.Notes,
                status = quotation.Status,
                createdAt = quotation.CreatedAt,
                expiresAt = quotation.ExpiresAt
            });
        }

        // GET: api/Quotations/my-quotations
        [HttpGet("my-quotations")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyQuotations()
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

            var quotations = await _context.Quotations
                .AsNoTracking()
                .Where(q =>
                    q.CustomCakeRequest.CustomerId == customerId)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new
                {
                    q.QuotationId,
                    q.CustomCakeRequestId,
                    q.QuotedAmount,
                    q.Notes,
                    q.Status,
                    q.CreatedAt,
                    q.ExpiresAt,
                    CakeSize = q.CustomCakeRequest.CakeSize,
                    Flavour = q.CustomCakeRequest.Flavour
                })
                .ToListAsync();

            return Ok(quotations);
        }

        // GET: api/Quotations/{id}
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Customer,Admin,Consultant")]
        public async Task<IActionResult> GetQuotation(int id)
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

            var isAdmin = User.IsInRole("Admin");
            var isConsultant = User.IsInRole("Consultant");

            var quotation = await _context.Quotations
                .AsNoTracking()
                .Where(q => q.QuotationId == id)
                .Where(q =>
                    isAdmin ||
                    (isConsultant &&
                     q.ConsultantId == userId) ||
                    q.CustomCakeRequest.CustomerId == userId)
                .Select(q => new
                {
                    q.QuotationId,
                    q.CustomCakeRequestId,
                    q.ConsultantId,
                    q.QuotedAmount,
                    q.Notes,
                    q.Status,
                    q.CreatedAt,
                    q.ExpiresAt
                })
                .FirstOrDefaultAsync();

            if (quotation == null)
            {
                return NotFound(new
                {
                    message = "Quotation not found."
                });
            }

            return Ok(quotation);
        }

        // PATCH: api/Quotations/{id}/respond
        [HttpPatch("{id:int}/respond")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RespondToQuotation(
            int id,
            [FromBody] RespondToQuotationDto dto)
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

            var quotation = await _context.Quotations
                .Include(q => q.CustomCakeRequest)
                .FirstOrDefaultAsync(q =>
                    q.QuotationId == id &&
                    q.CustomCakeRequest.CustomerId == customerId);

            if (quotation == null)
            {
                return NotFound(new
                {
                    message = "Quotation not found."
                });
            }

            if (quotation.Status != "Pending")
            {
                return BadRequest(new
                {
                    message =
                        "This quotation has already been responded to."
                });
            }

            if (quotation.ExpiresAt.HasValue &&
                quotation.ExpiresAt.Value <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message = "This quotation has expired."
                });
            }

            if (dto.Status != "Accepted" &&
                dto.Status != "Rejected")
            {
                return BadRequest(new
                {
                    message =
                        "Quotation response must be Accepted or Rejected."
                });
            }

            if (dto.Status == "Accepted")
            {
                var otherAcceptedQuotation =
                    await _context.Quotations.AnyAsync(q =>
                        q.CustomCakeRequestId ==
                            quotation.CustomCakeRequestId &&
                        q.QuotationId != quotation.QuotationId &&
                        q.Status == "Accepted");

                if (otherAcceptedQuotation)
                {
                    return Conflict(new
                    {
                        message =
                            "A quotation has already been accepted for this cake request."
                    });
                }
            }

            quotation.Status = dto.Status;

            if (dto.Status == "Accepted")
            {
                quotation.CustomCakeRequest.Status =
                    "Quote Accepted";
            }
            else
            {
                quotation.CustomCakeRequest.Status =
                    "Quote Rejected";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Quotation response saved successfully.",
                quotationId = quotation.QuotationId,
                status = quotation.Status,
                customCakeRequestId =
                    quotation.CustomCakeRequestId,
                requestStatus =
                    quotation.CustomCakeRequest.Status
            });
        }
    }
}

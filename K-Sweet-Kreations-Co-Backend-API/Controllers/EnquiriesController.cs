
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
    public class EnquiriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EnquiriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Enquiries
        // Guests and registered customers can submit enquiries.
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateEnquiry(
            [FromBody] CreateEnquiryDto dto)
        {
            int? customerId = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdValue = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (int.TryParse(userIdValue, out int parsedId)
                    && User.IsInRole("Customer"))
                {
                    customerId = parsedId;
                }
            }

            var enquiry = new Enquiry
            {
                CustomerId = customerId,
                Name = dto.Name.Trim(),
                Email = dto.Email.Trim(),
                Subject = dto.Subject.Trim(),
                Message = dto.Message.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Enquiries.Add(enquiry);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Enquiry submitted successfully.",
                enquiryId = enquiry.EnquiryId,
                status = enquiry.Status,
                createdAt = enquiry.CreatedAt
            });
        }

        // GET: api/Enquiries
        // Admin retrieves all enquiries.
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllEnquiries()
        {
            var enquiries = await _context.Enquiries
                .AsNoTracking()
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new EnquiryResponseDto
                {
                    EnquiryId = e.EnquiryId,
                    CustomerId = e.CustomerId,
                    Name = e.Name,
                    Email = e.Email,
                    Subject = e.Subject,
                    Message = e.Message,
                    Status = e.Status,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return Ok(enquiries);
        }

        // GET: api/Enquiries/my-enquiries
        // Customers retrieve their own enquiries.
        [HttpGet("my-enquiries")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyEnquiries()
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

            var enquiries = await _context.Enquiries
                .AsNoTracking()
                .Where(e => e.CustomerId == customerId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new EnquiryResponseDto
                {
                    EnquiryId = e.EnquiryId,
                    CustomerId = e.CustomerId,
                    Name = e.Name,
                    Email = e.Email,
                    Subject = e.Subject,
                    Message = e.Message,
                    Status = e.Status,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return Ok(enquiries);
        }

        // GET: api/Enquiries/1
        // Admin or enquiry owner retrieves an enquiry.
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Customer")]
        public async Task<IActionResult> GetEnquiry(int id)
        {
            var enquiry = await _context.Enquiries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EnquiryId == id);

            if (enquiry == null)
            {
                return NotFound(new
                {
                    message = "Enquiry not found."
                });
            }

            if (!User.IsInRole("Admin"))
            {
                var userIdValue = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (!int.TryParse(userIdValue, out int customerId))
                {
                    return Unauthorized();
                }

                if (enquiry.CustomerId != customerId)
                {
                    return NotFound(new
                    {
                        message = "Enquiry not found."
                    });
                }
            }

            return Ok(new EnquiryResponseDto
            {
                EnquiryId = enquiry.EnquiryId,
                CustomerId = enquiry.CustomerId,
                Name = enquiry.Name,
                Email = enquiry.Email,
                Subject = enquiry.Subject,
                Message = enquiry.Message,
                Status = enquiry.Status,
                CreatedAt = enquiry.CreatedAt
            });
        }

        // PATCH: api/Enquiries/1/status
        // Admin updates an enquiry's status.
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEnquiryStatus(
            int id,
            [FromBody] UpdateEnquiryStatusDto dto)
        {
            var enquiry = await _context.Enquiries
                .FirstOrDefaultAsync(e => e.EnquiryId == id);

            if (enquiry == null)
            {
                return NotFound(new
                {
                    message = "Enquiry not found."
                });
            }

            var allowedStatuses = new[]
            {
                "Pending",
                "In Progress",
                "Resolved"
            };

            var requestedStatus = dto.Status?.Trim();

            if (string.IsNullOrWhiteSpace(requestedStatus))
            {
                return BadRequest(new
                {
                    message = "Enquiry status is required."
                });
            }

            var validStatus = allowedStatuses
                .FirstOrDefault(s =>
                    string.Equals(
                        s,
                        requestedStatus,
                        StringComparison.OrdinalIgnoreCase));

            if (validStatus == null)
            {
                return BadRequest(new
                {
                    message = "Invalid enquiry status.",
                    allowedStatuses
                });
            }

            var previousStatus = enquiry.Status;

            if (previousStatus == validStatus)
            {
                return Conflict(new
                {
                    message = "Enquiry already has this status."
                });
            }

            enquiry.Status = validStatus;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Enquiry status updated successfully.",
                enquiryId = enquiry.EnquiryId,
                previousStatus,
                status = enquiry.Status
            });
        }
    }
}

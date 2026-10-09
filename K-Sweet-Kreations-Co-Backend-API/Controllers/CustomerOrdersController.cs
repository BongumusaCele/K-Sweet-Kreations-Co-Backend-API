
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
    public class CustomerOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CustomerOrdersController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/CustomerOrders/from-quotation
        [HttpPost("from-quotation")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateFromQuotation(
            [FromBody] CreateOrderFromQuotationDto dto)
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
                    q.QuotationId == dto.QuotationId);

            if (quotation == null)
            {
                return NotFound(new
                {
                    message = "Quotation not found."
                });
            }

            if (quotation.CustomCakeRequest.CustomerId != customerId)
            {
                return NotFound(new
                {
                    message = "Quotation not found."
                });
            }

            if (quotation.Status != "Accepted")
            {
                return BadRequest(new
                {
                    message = "Only accepted quotations can become orders."
                });
            }

            if (quotation.ExpiresAt.HasValue &&
                quotation.ExpiresAt.Value <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message = "Quotation has expired."
                });
            }

            if (quotation.CustomCakeRequest.CollectionDateTime
                <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    message = "Collection date must be in the future."
                });
            }

            var existingOrder = await _context.CustomerOrders
                .AnyAsync(o => o.QuotationId == dto.QuotationId);

            if (existingOrder)
            {
                return Conflict(new
                {
                    message = "An order already exists for this quotation."
                });
            }

            var order = new CustomerOrder
            {
                CustomerId = customerId,
                QuotationId = quotation.QuotationId,
                TotalAmount = quotation.QuotedAmount,
                CollectionDateTime =
                    quotation.CustomCakeRequest.CollectionDateTime,
                OrderStatus = "Pending Deposit",
                CreatedAt = DateTime.UtcNow
            };

            _context.CustomerOrders.Add(order);

            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Customer order created successfully.",
                orderId = order.OrderId,
                customerId = order.CustomerId,
                quotationId = order.QuotationId,
                totalAmount = order.TotalAmount,
                depositRequired =
                    Math.Round(order.TotalAmount * 0.50m, 2),
                remainingBalance =
                    Math.Round(order.TotalAmount * 0.50m, 2),
                orderStatus = order.OrderStatus,
                collectionDateTime = order.CollectionDateTime,
                createdAt = order.CreatedAt
            });
        }
    }
}

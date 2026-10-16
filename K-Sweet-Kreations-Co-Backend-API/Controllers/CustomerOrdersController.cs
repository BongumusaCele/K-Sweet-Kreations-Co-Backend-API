
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Observers;
using K_Sweet_Kreations_Co_Backend_API.Services;
using K_Sweet_Kreations_Co_Backend_API.Factories;
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
    public class CustomerOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly OrderSubject _orderSubject;
        private readonly OrderService _orderService;
        private readonly ICustomerOrderFactory _orderFactory;

        public CustomerOrdersController(
            ApplicationDbContext context,
            OrderSubject orderSubject,
            OrderService orderService,
            ICustomerOrderFactory orderFactory)
        {
            _context = context;
            _orderSubject = orderSubject;
            _orderService = orderService;
            _orderFactory = orderFactory;
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

            if (quotation == null ||
                quotation.CustomCakeRequest.CustomerId != customerId)
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

            // Strategy Pattern:
            // Calculate the order total using the pricing strategy.
            var totalAmount = _orderService.CalculateOrderTotal(
                quotation.QuotedAmount);

            // Factory Pattern:
            // Delegate CustomerOrder creation to the factory.
            var order = _orderFactory.CreateFromQuotation(
                customerId,
                quotation.QuotationId,
                totalAmount,
                quotation.CustomCakeRequest.CollectionDateTime);

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

        // GET: api/CustomerOrders/my-orders
        [HttpGet("my-orders")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyOrders()
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

            var orders = await _context.CustomerOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.OrderId,
                    o.QuotationId,
                    o.TotalAmount,
                    o.OrderStatus,
                    o.CollectionDateTime,
                    o.CreatedAt,
                    TotalPaid = o.Payments
                        .Where(p => p.PaymentStatus == "Completed")
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .ToListAsync();

            var results = orders.Select(o => new
            {
                o.OrderId,
                o.QuotationId,
                o.TotalAmount,
                o.OrderStatus,
                o.CollectionDateTime,
                o.CreatedAt,
                o.TotalPaid,
                OutstandingBalance =
                    Math.Max(0m, o.TotalAmount - o.TotalPaid)
            });

            return Ok(results);
        }

        // GET: api/CustomerOrders/{id}
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> GetOrderById(int id)
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

            var order = await _context.CustomerOrders
                .AsNoTracking()
                .Where(o => o.OrderId == id)
                .Where(o => isAdmin || o.CustomerId == userId)
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerId,
                    o.QuotationId,
                    o.TotalAmount,
                    o.OrderStatus,
                    o.CollectionDateTime,
                    o.CreatedAt,
                    Payments = o.Payments.Select(p => new
                    {
                        p.PaymentId,
                        p.Amount,
                        p.PaymentMethod,
                        p.PaymentStatus,
                        p.PaymentReference,
                        p.PaidAt,
                        p.CreatedAt
                    }).ToList(),
                    TotalPaid = o.Payments
                        .Where(p => p.PaymentStatus == "Completed")
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }

            return Ok(new
            {
                order.OrderId,
                order.CustomerId,
                order.QuotationId,
                order.TotalAmount,
                order.OrderStatus,
                order.CollectionDateTime,
                order.CreatedAt,
                order.Payments,
                order.TotalPaid,
                OutstandingBalance = Math.Max(
                    0m, order.TotalAmount - order.TotalPaid)
            });
        }

        // GET: api/CustomerOrders
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.CustomerOrders
                .AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerId,
                    CustomerName = o.Customer.FullName,
                    o.QuotationId,
                    o.TotalAmount,
                    o.OrderStatus,
                    o.CollectionDateTime,
                    o.CreatedAt,
                    TotalPaid = o.Payments
                        .Where(p => p.PaymentStatus == "Completed")
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .ToListAsync();

            var results = orders.Select(o => new
            {
                o.OrderId,
                o.CustomerId,
                o.CustomerName,
                o.QuotationId,
                o.TotalAmount,
                o.OrderStatus,
                o.CollectionDateTime,
                o.CreatedAt,
                o.TotalPaid,
                OutstandingBalance = Math.Max(
                    0m, o.TotalAmount - o.TotalPaid)
            });

            return Ok(results);
        }

        // PATCH: api/CustomerOrders/{id}/status
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "Admin,Consultant")]
        public async Task<IActionResult> UpdateOrderStatus(
            int id,
            [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _context.CustomerOrders
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }

            var newStatus = dto.Status?.Trim();

            if (string.IsNullOrWhiteSpace(newStatus))
            {
                return BadRequest(new
                {
                    message = "Order status is required."
                });
            }

            var allowedTransitions =
                new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Pending Deposit"] = new[]
                    {
                        "Cancelled"
                    },

                    ["Deposit Paid"] = new[]
                    {
                        "In Preparation",
                        "Cancelled"
                    },

                    ["Paid"] = new[]
                    {
                        "In Preparation"
                    },

                    ["In Preparation"] = new[]
                    {
                        "Ready for Collection"
                    },

                    ["Ready for Collection"] = new[]
                    {
                        "Collected"
                    }
                };

            var previousStatus = order.OrderStatus;

            var normalTransition =
                allowedTransitions.TryGetValue(
                    previousStatus,
                    out var nextStatuses) &&
                nextStatuses!.Contains(
                    newStatus,
                    StringComparer.OrdinalIgnoreCase);

            // Preserve the existing administrative recovery rule.
            bool isRecovery =
                User.IsInRole("Admin") &&
                string.Equals(
                    previousStatus,
                    "Paid",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    newStatus,
                    "Ready for Collection",
                    StringComparison.OrdinalIgnoreCase) &&
                await _context.OrderStatusHistories.AnyAsync(h =>
                    h.OrderId == id &&
                    h.Status == "Ready for Collection") &&
                (await _context.Payments
                    .Where(p =>
                        p.OrderId == id &&
                        p.PaymentStatus == "Completed")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m)
                    >= order.TotalAmount;

            if (!normalTransition && !isRecovery)
            {
                return BadRequest(new
                {
                    message = "Invalid order status transition.",
                    currentStatus = previousStatus,
                    requestedStatus = newStatus
                });
            }

            var canonicalStatus = isRecovery
                ? "Ready for Collection"
                : nextStatuses!.First(s =>
                    string.Equals(
                        s,
                        newStatus,
                        StringComparison.OrdinalIgnoreCase));

            order.OrderStatus = canonicalStatus;

            var history = new OrderStatusHistory
            {
                OrderId = order.OrderId,
                Status = canonicalStatus,
                Notes = dto.Notes?.Trim(),
                ChangedAt = DateTime.UtcNow
            };

            _context.OrderStatusHistories.Add(history);

            await _context.SaveChangesAsync();

            // Observer Pattern:
            // Notify observers after the status update is saved.
            var orderEvent = new OrderStatusChangedEvent(
                order.OrderId,
                order.CustomerId,
                previousStatus,
                canonicalStatus,
                history.ChangedAt
            );

            await _orderSubject.NotifyObserversAsync(orderEvent);

            return Ok(new
            {
                message = "Order status updated successfully.",
                orderId = order.OrderId,
                previousStatus,
                orderStatus = order.OrderStatus,
                historyId = history.OrderStatusHistoryId,
                changedAt = history.ChangedAt
            });
        }

        // GET: api/CustomerOrders/{id}/history
        [HttpGet("{id:int}/history")]
        [Authorize(Roles = "Customer,Admin,Consultant")]
        public async Task<IActionResult> GetOrderHistory(int id)
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

            var isStaff = User.IsInRole("Admin") ||
                          User.IsInRole("Consultant");

            var order = await _context.CustomerOrders
                .AsNoTracking()
                .Where(o => o.OrderId == id)
                .Where(o => isStaff || o.CustomerId == userId)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderStatus
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }

            var history = await _context.OrderStatusHistories
                .AsNoTracking()
                .Where(h => h.OrderId == id)
                .OrderBy(h => h.ChangedAt)
                .ThenBy(h => h.OrderStatusHistoryId)
                .Select(h => new
                {
                    h.OrderStatusHistoryId,
                    h.Status,
                    h.Notes,
                    h.ChangedAt
                })
                .ToListAsync();

            return Ok(new
            {
                order.OrderId,
                currentStatus = order.OrderStatus,
                statusHistory = history
            });
        }
    }
}

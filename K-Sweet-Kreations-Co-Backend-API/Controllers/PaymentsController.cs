
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
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Payments
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreatePayment(
            [FromBody] CreatePaymentDto dto)
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

            var order = await _context.CustomerOrders
                .FirstOrDefaultAsync(o =>
                    o.OrderId == dto.OrderId &&
                    o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound(new
                {
                    message = "Customer order not found."
                });
            }

            if (order.OrderStatus == "Cancelled")
            {
                return BadRequest(new
                {
                    message = "Cannot pay for a cancelled order."
                });
            }

            var paymentMethod = dto.PaymentMethod?.Trim();

            var allowedMethods = new[]
            {
                "EFT",
                "Bank Transfer"
            };

            if (string.IsNullOrWhiteSpace(paymentMethod) ||
                !allowedMethods.Contains(
                    paymentMethod,
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Unsupported payment method."
                });
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(new
                {
                    message = "Payment amount must be greater than zero."
                });
            }

            var confirmedTotal = await _context.Payments
                .Where(p =>
                    p.OrderId == order.OrderId &&
                    p.PaymentStatus == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var pendingTotal = await _context.Payments
                .Where(p =>
                    p.OrderId == order.OrderId &&
                    p.PaymentStatus == "Pending")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var outstandingBalance =
                order.TotalAmount - confirmedTotal;

            if (outstandingBalance <= 0)
            {
                return BadRequest(new
                {
                    message = "This order has already been paid in full."
                });
            }

            if (dto.Amount > outstandingBalance)
            {
                return BadRequest(new
                {
                    message = "Payment exceeds outstanding balance."
                });
            }

            if (dto.Amount + pendingTotal > outstandingBalance)
            {
                return Conflict(new
                {
                    message = "Payment exceeds the available balance when pending payments are included."
                });
            }

            var paymentReference =
                dto.PaymentReference?.Trim();

            if (!string.IsNullOrWhiteSpace(paymentReference))
            {
                var duplicateReference = await _context.Payments
                    .AnyAsync(p =>
                        p.PaymentReference == paymentReference &&
                        p.PaymentStatus != "Rejected");

                if (duplicateReference)
                {
                    return Conflict(new
                    {
                        message = "This payment reference has already been submitted."
                    });
                }
            }

            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = dto.Amount,
                PaymentMethod = paymentMethod,
                PaymentStatus = "Pending",
                PaymentReference = paymentReference,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Payment submitted for verification.",
                paymentId = payment.PaymentId,
                orderId = payment.OrderId,
                amount = payment.Amount,
                paymentMethod = payment.PaymentMethod,
                paymentStatus = payment.PaymentStatus,
                paymentReference = payment.PaymentReference,
                createdAt = payment.CreatedAt
            });
        }

        // PATCH: api/Payments/1/verify
        [HttpPatch("{id:int}/verify")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> VerifyPayment(
            int id,
            [FromBody] VerifyPaymentDto dto)
        {
            var requestedStatus = dto.Status?.Trim();

            if (requestedStatus != "Completed" &&
                requestedStatus != "Rejected")
            {
                return BadRequest(new
                {
                    message = "Status must be Completed or Rejected."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var payment = await _context.Payments
                .Include(p => p.CustomerOrder)
                .FirstOrDefaultAsync(p => p.PaymentId == id);

            if (payment == null)
            {
                return NotFound(new
                {
                    message = "Payment not found."
                });
            }

            if (payment.PaymentStatus != "Pending")
            {
                return Conflict(new
                {
                    message = "This payment has already been reviewed."
                });
            }

            var order = payment.CustomerOrder;

            if (order.OrderStatus == "Cancelled")
            {
                return BadRequest(new
                {
                    message = "Cannot verify payment for a cancelled order."
                });
            }

            if (requestedStatus == "Completed")
            {
                var confirmedTotal = await _context.Payments
                    .Where(p =>
                        p.OrderId == order.OrderId &&
                        p.PaymentStatus == "Completed")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                if (confirmedTotal + payment.Amount >
                    order.TotalAmount)
                {
                    return Conflict(new
                    {
                        message = "Confirming this payment would exceed the order total."
                    });
                }

                payment.PaymentStatus = "Completed";
                payment.PaidAt = DateTime.UtcNow;

                confirmedTotal += payment.Amount;

                var requiredDeposit =
                    Math.Round(order.TotalAmount * 0.50m, 2);

                // Only update payment-stage order statuses.
                // Never overwrite preparation or collection statuses.
                var paymentStageStatuses = new[]
                {
                    "Pending",
                    "Pending Deposit",
                    "Deposit Paid",
                    "Paid"
                };

                if (paymentStageStatuses.Contains(
                    order.OrderStatus,
                    StringComparer.OrdinalIgnoreCase))
                {
                    if (confirmedTotal >= order.TotalAmount)
                    {
                        order.OrderStatus = "Paid";
                    }
                    else if (confirmedTotal >= requiredDeposit)
                    {
                        order.OrderStatus = "Deposit Paid";
                    }
                }
            }
            else
            {
                payment.PaymentStatus = "Rejected";
                payment.PaidAt = null;
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Payment verification completed successfully.",
                paymentId = payment.PaymentId,
                orderId = order.OrderId,
                amount = payment.Amount,
                paymentStatus = payment.PaymentStatus,
                paidAt = payment.PaidAt,
                orderStatus = order.OrderStatus
            });
        }
    }
}

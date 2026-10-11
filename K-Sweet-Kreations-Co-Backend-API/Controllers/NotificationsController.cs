
using K_Sweet_Kreations_Co_Backend_API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // Get the authenticated customer's ID.
        private int? GetCustomerId()
        {
            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (int.TryParse(userIdValue, out int customerId))
            {
                return customerId;
            }

            return null;
        }

        // GET: api/Notifications/my-notifications
        [HttpGet("my-notifications")]
        public async Task<IActionResult> GetMyNotifications()
        {
            var customerId = GetCustomerId();

            if (customerId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid customer authentication."
                });
            }

            var notifications = await _context.CustomerNotifications
                .AsNoTracking()
                .Where(n => n.CustomerId == customerId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.NotificationId)
                .Select(n => new
                {
                    n.NotificationId,
                    n.OrderId,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // GET: api/Notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var customerId = GetCustomerId();

            if (customerId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid customer authentication."
                });
            }

            var unreadCount = await _context.CustomerNotifications
                .AsNoTracking()
                .CountAsync(n =>
                    n.CustomerId == customerId.Value &&
                    !n.IsRead);

            return Ok(new
            {
                unreadCount
            });
        }

        // PATCH: api/Notifications/{id}/read
        [HttpPatch("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var customerId = GetCustomerId();

            if (customerId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid customer authentication."
                });
            }

            var notification = await _context.CustomerNotifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.CustomerId == customerId.Value);

            if (notification == null)
            {
                return NotFound(new
                {
                    message = "Notification not found."
                });
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;

                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Notification marked as read.",
                notificationId = notification.NotificationId,
                isRead = notification.IsRead
            });
        }
    }
}

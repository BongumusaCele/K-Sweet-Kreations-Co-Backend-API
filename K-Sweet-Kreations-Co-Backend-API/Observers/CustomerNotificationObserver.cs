
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;

namespace K_Sweet_Kreations_Co_Backend_API.Observers
{
    public class CustomerNotificationObserver : IOrderObserver
    {
        private readonly ApplicationDbContext _context;

        public CustomerNotificationObserver(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnOrderStatusChangedAsync(
            OrderStatusChangedEvent orderEvent,
            CancellationToken cancellationToken = default)
        {
            string title = "Order Status Updated";

            string message = orderEvent.NewStatus switch
            {
                "Deposit Paid" =>
                    $"Your deposit for order #{orderEvent.OrderId} has been confirmed.",

                "Paid" =>
                    $"Full payment for order #{orderEvent.OrderId} has been confirmed.",

                "In Preparation" =>
                    $"Your order #{orderEvent.OrderId} is now being prepared.",

                "Ready for Collection" =>
                    $"Your order #{orderEvent.OrderId} is ready for collection.",

                "Collected" =>
                    $"Your order #{orderEvent.OrderId} has been collected.",

                "Cancelled" =>
                    $"Your order #{orderEvent.OrderId} has been cancelled.",

                _ =>
                    $"Your order #{orderEvent.OrderId} status has changed to {orderEvent.NewStatus}."
            };

            var notification = new CustomerNotification
            {
                CustomerId = orderEvent.CustomerId,
                OrderId = orderEvent.OrderId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.CustomerNotifications.Add(notification);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}


namespace K_Sweet_Kreations_Co_Backend_API.Observers
{
    public class OrderSubject
    {
        private readonly IEnumerable<IOrderObserver> _observers;

        public OrderSubject(IEnumerable<IOrderObserver> observers)
        {
            _observers = observers;
        }

        public async Task NotifyObserversAsync(
            OrderStatusChangedEvent orderEvent,
            CancellationToken cancellationToken = default)
        {
            foreach (var observer in _observers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await observer.OnOrderStatusChangedAsync(
                    orderEvent,
                    cancellationToken);
            }
        }
    }
}

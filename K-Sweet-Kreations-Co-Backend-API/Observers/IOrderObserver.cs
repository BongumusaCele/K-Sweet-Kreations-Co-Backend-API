namespace K_Sweet_Kreations_Co_Backend_API.Observers
{
    public interface IOrderObserver
    {
        Task OnOrderStatusChangedAsync(
            OrderStatusChangedEvent orderEvent,
            CancellationToken cancellationToken = default);
    }
}
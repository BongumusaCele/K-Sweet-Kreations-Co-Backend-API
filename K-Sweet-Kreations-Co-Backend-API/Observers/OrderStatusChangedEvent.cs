
namespace K_Sweet_Kreations_Co_Backend_API.Observers
{
    public sealed record OrderStatusChangedEvent(
        int OrderId,
        int CustomerId,
        string PreviousStatus,
        string NewStatus,
        DateTime ChangedAt
    );
}

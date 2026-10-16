using System.Globalization;
using K_Sweet_Kreations_Co_Backend_API.Models;

namespace K_Sweet_Kreations_Co_Backend_API.Services;

public class WebsiteException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class WebsiteRules
{
    public static DateTime SouthAfricaNow => DateTime.UtcNow.AddHours(2);
    public static DateTime Collection(string date, string time, bool custom, IEnumerable<string> blocked, DateTime? now = null)
    {
        if (!DateTime.TryParseExact($"{date} {time}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
            throw new WebsiteException(400, "Choose a valid collection date and time.");
        if (local.Date < (now ?? SouthAfricaNow).Date.AddDays(custom ? 7 : 1))
            throw new WebsiteException(400, custom ? "Custom cakes require seven days’ notice." : "Choose collection from tomorrow onwards.");
        if (blocked.Contains(date) || local.DayOfWeek == DayOfWeek.Sunday || local.Hour < 9 ||
            local.Hour >= (local.DayOfWeek == DayOfWeek.Saturday ? 15 : 17) || local.Minute % 30 != 0)
            throw new WebsiteException(400, "This collection slot is unavailable. Choose a time within bakery hours.");
        return DateTime.SpecifyKind(local.AddHours(-2), DateTimeKind.Utc);
    }
    public static decimal Deposit(decimal total) => decimal.Round(total / 2, 2, MidpointRounding.AwayFromZero);
    public static decimal Paid(CustomerOrder order) => order.Payments.Where(p => p.PaymentStatus == "Completed").Sum(p => p.Amount);
    public static void Transition(CustomerOrder order, string next, bool admin)
    {
        if (order.OrderStatus is "Cancelled" or "Collected") throw new WebsiteException(409, "This order is closed.");
        if (next == "Cancelled")
        {
            if (!admin) throw new WebsiteException(403, "Only Admin can approve cancellations.");
            return;
        }
        var expected = order.OrderStatus switch
        {
            "Awaiting confirmation" => "Confirmed",
            "Confirmed" => "In progress",
            "In progress" => "Ready for collection",
            "Ready for collection" => "Collected",
            _ => ""
        };
        if (next != expected) throw new WebsiteException(409, "Complete the previous order stage first.");
        var paid = Paid(order);
        if (next == "Confirmed" && (!order.SlotApproved || paid < (order.QuotationId.HasValue ? Deposit(order.TotalAmount) : order.TotalAmount)))
            throw new WebsiteException(409, "Confirmation requires approved collection and verified payment.");
        if (next == "Collected" && paid < order.TotalAmount) throw new WebsiteException(409, "Full payment is required before collection.");
    }
}

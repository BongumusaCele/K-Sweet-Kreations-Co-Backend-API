using System.ComponentModel.DataAnnotations;
using System.Globalization;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Services;

namespace K_Sweet_Kreations_Co_Tests;

public class WebsiteRulesTests
{
    [Theory]
    [InlineData("2026-10-18", "10:00", false)]
    [InlineData("2026-10-19", "17:00", false)]
    [InlineData("2026-10-17", "15:00", false)]
    [InlineData("2026-10-19", "10:15", false)]
    [InlineData("2026-10-16", "10:00", true)]
    [InlineData("invalid", "10:00", false)]
    public void InvalidCollectionIsRejected(string date, string time, bool custom)
        => Assert.Throws<WebsiteException>(() => WebsiteRules.Collection(date, time, custom, [], new DateTime(2026,10,15)));

    [Fact]
    public void CollectionUsesSouthAfricanTimeAndBlockedDates()
    {
        var utc = WebsiteRules.Collection("2026-10-23", "10:30", true, [], new DateTime(2026,10,15));
        Assert.Equal(new DateTime(2026,10,23,8,30,0,DateTimeKind.Utc), utc);
        Assert.Throws<WebsiteException>(() => WebsiteRules.Collection("2026-10-23", "10:30", true, ["2026-10-23"], new DateTime(2026,10,15)));
    }
    [Fact]
    public void PendingProofIsNotPaidAndCustomDepositRoundsAwayFromZero()
    {
        var order = new CustomerOrder { TotalAmount = 100.01m };
        order.Payments.Add(new Payment { Amount = 50.01m, PaymentStatus = "Pending" });
        Assert.Equal(0, WebsiteRules.Paid(order));
        Assert.Equal(50.01m, WebsiteRules.Deposit(order.TotalAmount));
    }
    [Fact]
    public void ConfirmationRequiresSlotAndVerifiedPayment()
    {
        var order = new CustomerOrder { TotalAmount = 100, QuotationId = 1, OrderStatus = "Awaiting confirmation" };
        order.Payments.Add(new Payment { Amount = 50, PaymentStatus = "Completed" });
        Assert.Throws<WebsiteException>(() => WebsiteRules.Transition(order, "Confirmed", false));
        order.SlotApproved = true;
        WebsiteRules.Transition(order, "Confirmed", false);
    }
    [Fact]
    public void UnpaidHandoverAndStaffCancellationAreRejected()
    {
        var order = new CustomerOrder { TotalAmount = 100, OrderStatus = "Ready for collection" };
        order.Payments.Add(new Payment { Amount = 50, PaymentStatus = "Completed" });
        Assert.Throws<WebsiteException>(() => WebsiteRules.Transition(order, "Collected", true));
        Assert.Throws<WebsiteException>(() => WebsiteRules.Transition(order, "Cancelled", false));
        WebsiteRules.Transition(order, "Cancelled", true);
    }
    [Theory]
    [InlineData("Collected")]
    [InlineData("Cancelled")]
    public void ClosedOrdersCannotReopen(string status)
        => Assert.Throws<WebsiteException>(() => WebsiteRules.Transition(new CustomerOrder { OrderStatus = status }, "Confirmed", true));

    [Theory]
    [InlineData("en-ZA")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void MoneyValidationWorksAcrossServerCultures(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var dto = new WebsiteQuoteDto { Amount = 100.01m, Notes = "Approved design" };
            Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), [], true));
            dto.Amount = -1;
            Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), [], true));
        } finally { CultureInfo.CurrentCulture = previous; }
    }
}


using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class CreatePaymentDto
    {
        [Required(ErrorMessage = "Order ID is required.")]
        [Range(1, int.MaxValue,
            ErrorMessage = "Order ID must be greater than zero.")]
        public int OrderId { get; set; }

        [Required(ErrorMessage = "Payment amount is required.")]
        [Range(
            typeof(decimal),
            "0.01",
            "99999999.99",
            ErrorMessage = "Payment amount must be between 0.01 and 99999999.99.",
            ParseLimitsInInvariantCulture = true)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [StringLength(30,
            ErrorMessage = "Payment method cannot exceed 30 characters.")]
        [RegularExpression(
            @"^(EFT|Bank Transfer)$",
            ErrorMessage = "Payment method must be EFT or Bank Transfer.")]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(200,
            ErrorMessage = "Payment reference cannot exceed 200 characters.")]
        public string? PaymentReference { get; set; }
    }
}

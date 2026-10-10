
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class CreatePaymentDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int OrderId { get; set; }

        [Required]
        [Range(
            typeof(decimal),
            "0.01",
            "99999999.99",
            ParseLimitsInInvariantCulture = true)]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(30)]
        public string PaymentMethod { get; set; }
            = string.Empty;

        [MaxLength(200)]
        public string? PaymentReference { get; set; }
    }
}

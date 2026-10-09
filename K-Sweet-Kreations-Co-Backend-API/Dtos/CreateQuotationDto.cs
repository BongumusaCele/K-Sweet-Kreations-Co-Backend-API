
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class CreateQuotationDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int CustomCakeRequestId { get; set; }

        [Range(
    typeof(decimal),
    "0.01",
    "99999999.99",
    ParseLimitsInInvariantCulture = true,
    ErrorMessage = "Quoted amount must be between 0.01 and 99999999.99.")]
        public decimal QuotedAmount { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }
}

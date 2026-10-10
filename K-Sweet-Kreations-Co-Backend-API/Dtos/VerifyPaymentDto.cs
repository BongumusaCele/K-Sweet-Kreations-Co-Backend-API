
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class VerifyPaymentDto
    {
        [Required]
        [RegularExpression(
            "^(Completed|Rejected)$",
            ErrorMessage = "Status must be Completed or Rejected.")]
        public string Status { get; set; } = string.Empty;
    }
}

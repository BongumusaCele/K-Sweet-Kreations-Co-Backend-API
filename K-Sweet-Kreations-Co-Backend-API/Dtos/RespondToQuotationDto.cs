
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class RespondToQuotationDto
    {
        [Required]
        [RegularExpression(
            "^(Accepted|Rejected)$",
            ErrorMessage = "Status must be Accepted or Rejected.")]
        public string Status { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class UpdateCustomCakeStatusDto
    {
        [Required]
        [RegularExpression(
            "^(Pending|Under Review|Quoted|Rejected)$",
            ErrorMessage = "Invalid custom cake request status.")]
        public string Status { get; set; } = string.Empty;
    }
}

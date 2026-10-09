
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class CreateCustomCakeRequestDto
    {
        [Required]
        [MaxLength(50)]
        public string CakeSize { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Flavour { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Filling { get; set; }

        [MaxLength(1000)]
        public string? CustomizationDetails { get; set; }

        [Required]
        public DateTime CollectionDateTime { get; set; }
    }
}

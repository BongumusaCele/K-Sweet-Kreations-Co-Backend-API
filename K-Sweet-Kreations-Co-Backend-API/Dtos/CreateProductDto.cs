
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class CreateProductDto
    {
        [Required]
        [MaxLength(150)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [Range(
    typeof(decimal),
    "0.01",
    "99999999.99",
    ParseLimitsInInvariantCulture = true)]
        public decimal StartingPrice { get; set; }

        public bool Available { get; set; } = true;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class CustomCakeImage
    {
        [Key]
        public int CustomCakeImageId { get; set; }

        [Required]
        public int CustomCakeRequestId { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CustomCakeRequestId))]
        public CustomCakeRequest CustomCakeRequest { get; set; } = null!;
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class CustomCakeRequest
    {
        [MaxLength(100)] public string ContactName { get; set; } = "";
        [MaxLength(20)] public string ContactPhone { get; set; } = "";

        [Key]
        public int CustomCakeRequestId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        public int? ConsultantId { get; set; }

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

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CustomerId))]
        public UserAccount Customer { get; set; } = null!;

        [ForeignKey(nameof(ConsultantId))]
        public UserAccount? Consultant { get; set; }

        public ICollection<CustomCakeImage> CustomCakeImages { get; set; }
            = new List<CustomCakeImage>();

        public ICollection<Quotation> Quotations { get; set; }
            = new List<Quotation>();
    }
}
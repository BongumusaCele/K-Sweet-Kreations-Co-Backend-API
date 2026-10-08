using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class Quotation
    {
        [Key]
        public int QuotationId { get; set; }

        [Required]
        public int CustomCakeRequestId { get; set; }

        [Required]
        public int ConsultantId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal QuotedAmount { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        [ForeignKey(nameof(CustomCakeRequestId))]
        public CustomCakeRequest CustomCakeRequest { get; set; } = null!;

        [ForeignKey(nameof(ConsultantId))]
        public UserAccount Consultant { get; set; } = null!;
    }
}
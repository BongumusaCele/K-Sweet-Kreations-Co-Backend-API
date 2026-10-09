using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class OrderStatusHistory
    {
        [Key]
        public int OrderStatusHistoryId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(OrderId))]
        public CustomerOrder CustomerOrder { get; set; } = null!;
    }
}

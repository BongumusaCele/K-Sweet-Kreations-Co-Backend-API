using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class CustomerOrder
    {
        [MaxLength(100)] public string ContactName { get; set; } = "";
        [MaxLength(20)] public string ContactPhone { get; set; } = "";
        [MaxLength(1000)] public string Instructions { get; set; } = "";
        public bool SlotApproved { get; set; }
        public bool CancelRequested { get; set; }
        public int? RequestId { get; set; }
        [Timestamp] public byte[]? Version { get; set; }

        [Key]
        public int OrderId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        public int? QuotationId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string OrderStatus { get; set; } = "Pending";

        [Required]
        public DateTime CollectionDateTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CustomerId))]
        public UserAccount Customer { get; set; } = null!;

        [ForeignKey(nameof(QuotationId))]
        public Quotation? Quotation { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();

        public ICollection<Payment> Payments { get; set; }
            = new List<Payment>();

        public ICollection<OrderStatusHistory> OrderStatusHistory { get; set; }
            = new List<OrderStatusHistory>();
    }
}

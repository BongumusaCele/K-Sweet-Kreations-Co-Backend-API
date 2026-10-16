
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class CustomerNotification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public UserAccount Customer { get; set; } = null!;

        public CustomerOrder CustomerOrder { get; set; } = null!;

    }
}

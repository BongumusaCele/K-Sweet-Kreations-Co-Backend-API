using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Models
{
    public class UserAccount
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Customer";

        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        // Navigation Properties

        public ICollection<UserSession> UserSessions { get; set; }
            = new List<UserSession>();

        public ICollection<CustomCakeRequest> CustomerCakeRequests { get; set; }
            = new List<CustomCakeRequest>();

        public ICollection<CustomCakeRequest> ConsultantCakeRequests { get; set; }
            = new List<CustomCakeRequest>();

        public ICollection<Quotation> Quotations { get; set; }
            = new List<Quotation>();

        public ICollection<CustomerOrder> CustomerOrders { get; set; }
            = new List<CustomerOrder>();

        public ICollection<Enquiry> Enquiries { get; set; }
            = new List<Enquiry>();
    }
}
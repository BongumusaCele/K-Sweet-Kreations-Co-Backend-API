
namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class EnquiryResponseDto
    {
        public int EnquiryId { get; set; }

        public int? CustomerId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}

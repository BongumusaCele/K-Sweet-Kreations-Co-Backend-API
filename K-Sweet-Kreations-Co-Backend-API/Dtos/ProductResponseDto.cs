
namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class ProductResponseDto
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public decimal StartingPrice { get; set; }

        public bool Available { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

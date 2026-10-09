
using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class CreateOrderFromQuotationDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int QuotationId { get; set; }
    }
}

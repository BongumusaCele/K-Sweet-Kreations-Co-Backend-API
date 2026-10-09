using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos
{
    public class AssignConsultantDto
    {
        [Range(1, int.MaxValue,
            ErrorMessage = "A valid Consultant ID is required.")]
        public int ConsultantId { get; set; }
    }
}
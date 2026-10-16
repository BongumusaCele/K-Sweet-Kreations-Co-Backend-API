using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Models;

// Persisted with the order/request so a network retry cannot create a second record.
public class WebsiteSubmission
{
    public Guid Id { get; set; }
    public int CustomerId { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "";
    [MaxLength(64)] public string PayloadHash { get; set; } = "";
}

using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Models;

public class BlockedCollectionDate
{
    public int Id { get; set; }
    [MaxLength(10)] public string Date { get; set; } = "";
}

public class CustomerReview
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    public int Rating { get; set; }
    [MaxLength(1000)] public string Message { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

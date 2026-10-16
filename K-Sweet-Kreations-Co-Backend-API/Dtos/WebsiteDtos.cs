using System.ComponentModel.DataAnnotations;

namespace K_Sweet_Kreations_Co_Backend_API.Dtos;

public class ContactDetails
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, RegularExpression(@"\+?(?=(?:.*[0-9]){7})[0-9 ()-]{7,20}")] public string Phone { get; set; } = "";
}
public class CollectionDetails : ContactDetails
{
    public Guid SubmissionId { get; set; }
    [Required] public string Date { get; set; } = "";
    [Required] public string Time { get; set; } = "";
}
public class WebsiteRequestDto : CollectionDetails
{
    [Required, StringLength(50)] public string Size { get; set; } = "";
    [Required, StringLength(100)] public string Flavour { get; set; } = "";
    [Required, StringLength(100)] public string Filling { get; set; } = "";
    [StringLength(1000)] public string Comments { get; set; } = "";
}
public class CheckoutItem
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Range(1, 20)] public int Quantity { get; set; }
}
public class WebsiteCheckoutDto : CollectionDetails
{
    [Required, MinLength(1), MaxLength(50)] public List<CheckoutItem> Items { get; set; } = [];
    [StringLength(1000)] public string Instructions { get; set; } = "";
}
public class WebsiteQuoteDto
{
    [Range(typeof(decimal), "0.01", "99999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal Amount { get; set; }
    [Required, StringLength(1000)] public string Notes { get; set; } = "";
}
public class WebsiteStatusDto
{
    [Required, StringLength(30)] public string Status { get; set; } = "";
}
public class WebsiteProductDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required, RegularExpression("^(Cakes|Cupcakes|Desserts)$")] public string Category { get; set; } = "";
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(K_Sweet_Kreations_Co_Backend_API.Services.InvariantDecimalBinder))]
    [Range(typeof(decimal), "0.01", "99999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal Price { get; set; }
    [Required, StringLength(100)] public string Size { get; set; } = "";
    [Required, StringLength(100)] public string Servings { get; set; } = "";
    [StringLength(500)] public string Image { get; set; } = "";
    public bool Active { get; set; } = true;
}
public class WebsiteEnquiryDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(200)] public string Subject { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 10)] public string Message { get; set; } = "";
}
public class WebsiteReviewDto
{
    [Required, StringLength(60)] public string Name { get; set; } = "";
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(1000)] public string Message { get; set; } = "";
}

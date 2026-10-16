using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers;

[ApiController, Route("api/website"), Authorize]
public class WebsiteController(ApplicationDbContext db, WebsiteService service, PrivateImageStore images) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool Team => User.IsInRole("Admin") || User.IsInRole("Staff");
    private async Task<bool> Replayed(string kind, CollectionDetails dto)
    {
        if (dto.SubmissionId == Guid.Empty) throw new WebsiteException(400, "A submission identifier is required. Reload the form and try again.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(dto, dto.GetType())));
        var previous = await db.WebsiteSubmissions.FindAsync(UserId, kind, dto.SubmissionId);
        if (previous != null)
        {
            if (previous.PayloadHash != hash) throw new WebsiteException(409, "This submission was already saved with different details. Check your orders before starting a new form.");
            return true;
        }
        db.WebsiteSubmissions.Add(new WebsiteSubmission { CustomerId = UserId, Kind = kind, Id = dto.SubmissionId, PayloadHash = hash });
        return false;
    }
    // Every mutation runs in a serializable transaction. Unique indexes protect
    // quotation conversion and payment references even across simultaneous requests.
    private async Task<IActionResult> Write(Func<Task> operation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await operation();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Snapshot()
    {
        if (Request.Cookies.ContainsKey("ksk_session") && User.Identity?.IsAuthenticated != true)
        {
            Response.Cookies.Delete("ksk_session", new CookieOptions { Path = "/api" });
            return Unauthorized(new { message = "Your session has expired. Please sign in again." });
        }
        return Ok(await service.Snapshot(User.Identity?.IsAuthenticated == true ? UserId : null, User.FindFirstValue(ClaimTypes.Role)));
    }

    [HttpPost("orders"), Authorize(Roles = "Customer")]
    public Task<IActionResult> Checkout(WebsiteCheckoutDto dto) => Write(async () =>
    {
        if (await Replayed("order", dto)) return;
        if (dto.Items.Select(i => i.Id).Distinct().Count() != dto.Items.Count) throw new WebsiteException(400, "Combine duplicate cart items.");
        var date = WebsiteRules.Collection(dto.Date, dto.Time, false, await service.Blocked());
        var ids = dto.Items.Select(i => i.Id).ToArray();
        var products = await db.Products.Where(p => ids.Contains(p.ProductId) && p.Available && p.DeletedAt == null).ToListAsync();
        if (products.Count != dto.Items.Count) throw new WebsiteException(409, "An item is no longer available. Update your cart.");
        var order = new CustomerOrder
        {
            CustomerId = UserId,
            ContactName = dto.Name.Trim(),
            ContactPhone = dto.Phone.Trim(),
            Instructions = dto.Instructions.Trim(),
            CollectionDateTime = date,
            OrderStatus = "Awaiting confirmation"
        };
        foreach (var item in dto.Items)
        {
            var product = products.Single(p => p.ProductId == item.Id);
            if (product.StartingPrice <= 0) throw new WebsiteException(409, "A product has no valid price. Contact the bakery.");
            order.OrderItems.Add(new OrderItem { ProductId = product.ProductId, UnitPrice = product.StartingPrice, Quantity = item.Quantity });
        }
        order.TotalAmount = order.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
        if (order.TotalAmount > 99999999.99m) throw new WebsiteException(400, "This order exceeds the maximum total. Contact the bakery.");
        WebsiteService.History(order, order.OrderStatus, UserId);
        db.CustomerOrders.Add(order);
    });

    [HttpPost("requests"), Authorize(Roles = "Customer"), RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> CreateRequest([FromForm] WebsiteRequestDto dto, IFormFile? file)
    {
        string? stored = null;
        try
        {
            return await Write(async () =>
            {
                if (await Replayed("request", dto)) return;
                var date = WebsiteRules.Collection(dto.Date, dto.Time, true, await service.Blocked());
                if (file != null) stored = await images.Save(file, "custom-cakes");
                var request = new CustomCakeRequest
                {
                    CustomerId = UserId,
                    ContactName = dto.Name.Trim(),
                    ContactPhone = dto.Phone.Trim(),
                    CakeSize = dto.Size.Trim(),
                    Flavour = dto.Flavour.Trim(),
                    Filling = dto.Filling.Trim(),
                    CustomizationDetails = dto.Comments.Trim(),
                    CollectionDateTime = date,
                    Status = "Submitted"
                };
                if (stored != null) request.CustomCakeImages.Add(new CustomCakeImage { StoredFileName = stored, ImageUrl = "private" });
                db.CustomCakeRequests.Add(request);
            });
        }
        catch { if (stored != null) await images.Delete("custom-cakes", stored); throw; }
    }

    [HttpPost("requests/{id:int}/quote"), Authorize(Roles = "Admin,Staff")]
    public Task<IActionResult> Quote(int id, WebsiteQuoteDto dto) => Write(async () =>
    {
        var request = await service.Request(id, UserId, true);
        if (request.Status is not ("Submitted" or "Under review" or "Quoted" or "Expired")) throw new WebsiteException(409, "This request cannot be quoted.");
        if (request.CollectionDateTime <= DateTime.UtcNow) throw new WebsiteException(409, "Arrange a future collection date before quoting.");
        var local = request.CollectionDateTime.AddHours(2);
        if ((await service.Blocked()).Contains(local.ToString("yyyy-MM-dd"))) throw new WebsiteException(409, "Collection date is blocked.");
        foreach (var old in request.Quotations.Where(q => q.Status == "Quoted")) old.Status = "Superseded";
        request.Quotations.Add(new Quotation
        {
            ConsultantId = UserId,
            QuotedAmount = decimal.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
            Notes = dto.Notes.Trim(),
            Status = "Quoted",
            ExpiresAt = DateTime.UtcNow.AddDays(3)
        });
        request.Status = "Quoted";
    });

    [HttpPatch("requests/{id:int}/status"), Authorize(Roles = "Admin,Staff")]
    public Task<IActionResult> RequestStatus(int id, WebsiteStatusDto dto) => Write(async () =>
    {
        var request = await service.Request(id, UserId, true);
        if (dto.Status is not ("Under review" or "Declined") || request.Status is "Accepted" or "Declined") throw new WebsiteException(409, "Invalid request transition.");
        request.Status = dto.Status;
        foreach (var q in request.Quotations.Where(q => q.Status == "Quoted")) q.Status = "Superseded";
    });

    [HttpPost("requests/{id:int}/respond"), Authorize(Roles = "Customer")]
    public Task<IActionResult> Respond(int id, WebsiteStatusDto dto) => Write(async () =>
    {
        var request = await service.Request(id, UserId, false);
        var quote = request.Quotations.OrderByDescending(q => q.CreatedAt).ThenByDescending(q => q.QuotationId).FirstOrDefault();
        if (request.Status != "Quoted" || quote?.Status != "Quoted" || quote.ExpiresAt == null || quote.ExpiresAt <= DateTime.UtcNow)
            throw new WebsiteException(409, "This quotation is no longer available. Ask the bakery to review it.");
        if (dto.Status is not ("Accepted" or "Declined")) throw new WebsiteException(400, "Accept or decline the quotation.");
        if (dto.Status == "Accepted")
        {
            if (request.CollectionDateTime <= DateTime.UtcNow || (await service.Blocked()).Contains(WebsiteService.Date(request.CollectionDateTime)))
                throw new WebsiteException(409, "Collection is no longer available. Contact the bakery.");
            if (await db.CustomerOrders.AnyAsync(o => o.RequestId == id || o.QuotationId == quote.QuotationId)) throw new WebsiteException(409, "An order already exists for this request.");
            var order = new CustomerOrder
            {
                CustomerId = UserId,
                QuotationId = quote.QuotationId,
                RequestId = id,
                ContactName = request.ContactName,
                ContactPhone = request.ContactPhone,
                Instructions = request.CustomizationDetails ?? "",
                TotalAmount = quote.QuotedAmount,
                CollectionDateTime = request.CollectionDateTime,
                OrderStatus = "Awaiting confirmation",
                SlotApproved = true
            };
            WebsiteService.History(order, order.OrderStatus, UserId);
            db.CustomerOrders.Add(order);
        }
        request.Status = quote.Status = dto.Status;
    });

    [HttpPatch("orders/{id:int}/status"), Authorize(Roles = "Admin,Staff")]
    public Task<IActionResult> OrderStatus(int id, WebsiteStatusDto dto) => Write(async () =>
    {
        var order = await service.Order(id, UserId, true);
        WebsiteRules.Transition(order, dto.Status, User.IsInRole("Admin"));
        order.OrderStatus = dto.Status;
        WebsiteService.History(order, dto.Status, UserId);
    });

    [HttpPost("orders/{id:int}/approve-slot"), Authorize(Roles = "Admin,Staff")]
    public Task<IActionResult> ApproveSlot(int id) => Write(async () =>
    {
        var order = await service.Order(id, UserId, true);
        if (order.OrderStatus is "Cancelled" or "Collected") throw new WebsiteException(409, "This order is closed.");
        if (order.CollectionDateTime <= DateTime.UtcNow || (await service.Blocked()).Contains(WebsiteService.Date(order.CollectionDateTime)))
            throw new WebsiteException(409, "Collection is unavailable. Contact the customer.");
        order.SlotApproved = true;
        WebsiteService.History(order, "Collection approved", UserId);
    });

    [HttpPost("orders/{id:int}/cancel"), Authorize(Roles = "Customer")]
    public Task<IActionResult> Cancel(int id) => Write(async () =>
    {
        var order = await service.Order(id, UserId, false);
        if (order.OrderStatus is "Cancelled" or "Collected") throw new WebsiteException(409, "This order is closed.");
        order.CancelRequested = true;
    });

    [HttpPost("orders/{id:int}/proof"), Authorize(Roles = "Customer"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Proof(int id, IFormFile file, [FromForm, System.ComponentModel.DataAnnotations.Required,
        System.ComponentModel.DataAnnotations.StringLength(100)] string reference)
    {
        string? stored = null;
        try
        {
            return await Write(async () =>
            {
                var order = await service.Order(id, UserId, false);
                var paid = WebsiteRules.Paid(order);
                if (order.OrderStatus is "Cancelled" or "Collected" || paid >= order.TotalAmount) throw new WebsiteException(409, "This order does not accept payments.");
                if (order.Payments.Any(p => p.PaymentStatus == "Pending")) throw new WebsiteException(409, "A payment is already awaiting verification.");
                var normalized = reference.Trim().ToUpperInvariant();
                if (await db.Payments.AnyAsync(p => p.PaymentReference == normalized)) throw new WebsiteException(409, "This payment reference has already been submitted.");
                stored = await images.Save(file, "payment-proofs");
                order.Payments.Add(new Payment
                {
                    Amount = order.QuotationId.HasValue && paid == 0 ? WebsiteRules.Deposit(order.TotalAmount) : order.TotalAmount - paid,
                    PaymentMethod = "EFT",
                    PaymentStatus = "Pending",
                    PaymentReference = normalized,
                    StoredProofName = stored
                });
            });
        }
        catch { if (stored != null) await images.Delete("payment-proofs", stored); throw; }
    }

    [HttpPatch("payments/{id:int}/verify"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Verify(int id, WebsiteStatusDto dto) => Write(async () =>
    {
        var payment = await db.Payments.Include(p => p.CustomerOrder).ThenInclude(o => o.Payments).FirstOrDefaultAsync(p => p.PaymentId == id)
            ?? throw new WebsiteException(404, "Payment not found.");
        if (dto.Status is not ("Verified" or "Rejected")) throw new WebsiteException(400, "Verify or reject the payment.");
        var order = payment.CustomerOrder;
        if (payment.PaymentStatus != "Pending" || order.OrderStatus == "Cancelled") throw new WebsiteException(409, "This payment cannot be reviewed.");
        if (dto.Status == "Verified" && WebsiteRules.Paid(order) + payment.Amount > order.TotalAmount) throw new WebsiteException(409, "Payment exceeds the outstanding balance.");
        payment.PaymentStatus = dto.Status == "Verified" ? "Completed" : "Rejected";
        payment.PaidAt = dto.Status == "Verified" ? DateTime.UtcNow : null;
        WebsiteService.History(order, dto.Status == "Verified" ? "EFT payment verified" : "EFT proof rejected", UserId);
    });

    [HttpGet("payments/{id:int}/proof")]
    public async Task<IActionResult> ProofFile(int id)
    {
        var payment = await db.Payments.Include(p => p.CustomerOrder).FirstOrDefaultAsync(p => p.PaymentId == id && (Team || p.CustomerOrder.CustomerId == UserId));
        if (payment?.StoredProofName == null) return NotFound();
        var stream = await images.Open("payment-proofs", payment.StoredProofName);
        if (stream == null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(stream, PrivateImageStore.Mime(payment.StoredProofName), $"EFT-proof-{payment.PaymentId}{Path.GetExtension(payment.StoredProofName)}");
    }

    [HttpPost("products/upload"), Authorize(Roles = "Admin"), RequestSizeLimit(3 * 1024 * 1024)]
    public Task<IActionResult> AddProductWithImage([FromForm] WebsiteProductDto dto, IFormFile? file) => SaveProductWithImage(null, dto, file);
    [HttpPut("products/{id:int}/upload"), Authorize(Roles = "Admin"), RequestSizeLimit(3 * 1024 * 1024)]
    public Task<IActionResult> UpdateProductWithImage(int id, [FromForm] WebsiteProductDto dto, IFormFile? file) => SaveProductWithImage(id, dto, file);
    private async Task<IActionResult> SaveProductWithImage(int? id, WebsiteProductDto dto, IFormFile? file)
    {
        string? stored = null;
        try
        {
            return await Write(async () => {
                var product = id.HasValue ? await db.Products.FindAsync(id.Value) ?? throw new WebsiteException(404, "Product not found.") : new Product();
                if (product.DeletedAt != null) throw new WebsiteException(404, "This product has been deleted.");
                if (file?.Length > 0) stored = await images.Save(file, "products");
                product.ProductName = dto.Name.Trim(); product.Description = dto.Description.Trim(); product.Category = dto.Category;
                product.StartingPrice = decimal.Round(dto.Price, 2, MidpointRounding.AwayFromZero); product.Available = dto.Active;
                product.Size = dto.Size.Trim(); product.Servings = dto.Servings.Trim();
                if (stored != null) product.Image = $"/uploads/products/{stored}";
                if (!id.HasValue) db.Products.Add(product);
            });
        }
        catch { if (stored != null) await images.Delete("products", stored); throw; }
    }

    [HttpPost("products"), Authorize(Roles = "Admin")]
    public Task<IActionResult> AddProduct([FromBody] WebsiteProductDto dto) => SaveProduct(null, dto);
    [HttpPut("products/{id:int}"), Authorize(Roles = "Admin")]
    public Task<IActionResult> UpdateProduct(int id, [FromBody] WebsiteProductDto dto) => SaveProduct(id, dto);
    private Task<IActionResult> SaveProduct(int? id, WebsiteProductDto dto) => Write(async () =>
    {
        if (dto.Image.Length > 0 && !dto.Image.StartsWith("/uploads/products/", StringComparison.Ordinal) && !dto.Image.StartsWith("/images/", StringComparison.Ordinal))
            throw new WebsiteException(400, "Use a bakery product image path.");
        var p = id.HasValue ? await db.Products.FindAsync(id.Value) ?? throw new WebsiteException(404, "Product not found.") : new Product();
        if (p.DeletedAt != null) throw new WebsiteException(404, "This product has been deleted.");
        p.ProductName = dto.Name.Trim(); p.Description = dto.Description.Trim(); p.Category = dto.Category;
        p.StartingPrice = decimal.Round(dto.Price, 2, MidpointRounding.AwayFromZero); p.Available = dto.Active;
        p.Size = dto.Size.Trim(); p.Servings = dto.Servings.Trim(); p.Image = dto.Image;
        if (!id.HasValue) db.Products.Add(p);
    });

    [HttpDelete("products/{id:int}"), Authorize(Roles = "Admin")]
    public Task<IActionResult> DeleteProduct(int id) => Write(async () =>
    {
        var product = await db.Products.FindAsync(id) ?? throw new WebsiteException(404, "Product not found.");
        // Keep the row and its media for historical order items; never cascade-delete orders.
        product.DeletedAt ??= DateTime.UtcNow;
        product.Available = false;
    });

    [HttpPut("blocked/{date}"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Block(string date) => Write(async () =>
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) || value.Date < WebsiteRules.SouthAfricaNow.Date)
            throw new WebsiteException(400, "Choose a valid future date.");
        if (await db.CustomerOrders.AnyAsync(o => o.CollectionDateTime >= value.AddHours(-2) && o.CollectionDateTime < value.AddDays(1).AddHours(-2) && o.OrderStatus != "Cancelled" && o.OrderStatus != "Collected"))
            throw new WebsiteException(409, "Existing orders use this date. Arrange their collections before blocking it.");
        if (!await db.BlockedCollectionDates.AnyAsync(d => d.Date == date)) db.BlockedCollectionDates.Add(new BlockedCollectionDate { Date = date });
    });
    [HttpDelete("blocked/{date}"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Unblock(string date) => Write(async () =>
    {
        var record = await db.BlockedCollectionDates.FirstOrDefaultAsync(d => d.Date == date);
        if (record != null) db.BlockedCollectionDates.Remove(record);
    });

    [HttpPost("enquiries"), AllowAnonymous]
    public Task<IActionResult> Enquiry(WebsiteEnquiryDto dto) => Write(() =>
    {
        db.Enquiries.Add(new Enquiry
        {
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Subject = dto.Subject.Trim(),
            Message = dto.Message.Trim(),
            Status = "New",
            CustomerId = User.Identity?.IsAuthenticated == true ? UserId : null
        });
        return Task.CompletedTask;
    });
    [HttpPatch("enquiries/{id:int}/status"), Authorize(Roles = "Admin,Staff")]
    public Task<IActionResult> EnquiryStatus(int id, WebsiteStatusDto dto) => Write(async () =>
    {
        if (dto.Status is not ("New" or "In progress" or "Resolved")) throw new WebsiteException(400, "Choose a valid enquiry status.");
        var e = await db.Enquiries.FindAsync(id) ?? throw new WebsiteException(404, "Enquiry not found."); e.Status = dto.Status;
    });
    [HttpPost("reviews"), Authorize(Roles = "Customer")]
    public Task<IActionResult> Review(WebsiteReviewDto dto) => Write(() =>
    {
        db.CustomerReviews.Add(new CustomerReview { CustomerId = UserId, Name = dto.Name.Trim(), Rating = dto.Rating, Message = dto.Message.Trim() });
        return Task.CompletedTask;
    });
    [HttpPatch("reviews/{id:int}/status"), Authorize(Roles = "Admin")]
    public Task<IActionResult> ReviewStatus(int id, WebsiteStatusDto dto) => Write(async () =>
    {
        if (dto.Status is not ("Published" or "Hidden")) throw new WebsiteException(400, "Publish or hide the feedback.");
        var r = await db.CustomerReviews.FindAsync(id) ?? throw new WebsiteException(404, "Feedback not found."); r.Status = dto.Status;
    });
}

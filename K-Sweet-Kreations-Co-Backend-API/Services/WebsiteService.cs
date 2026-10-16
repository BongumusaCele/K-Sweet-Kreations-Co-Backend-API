using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Services;

public class WebsiteService(ApplicationDbContext db)
{
    public static string Date(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(2).ToString("yyyy-MM-dd");
    public static string Time(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(2).ToString("HH:mm");
    public static object ProductView(Product p) => new
    {
        id = p.ProductId,
        name = p.ProductName,
        description = p.Description,
        category = p.Category,
        price = p.StartingPrice,
        active = p.Available,
        size = p.Size,
        servings = p.Servings,
        image = string.IsNullOrEmpty(p.Image) ? p.ProductImages.OrderByDescending(i => i.IsPrimary).FirstOrDefault()?.ImageUrl ?? "" : p.Image
    };

    public async Task<object> Snapshot(int? userId, string? role)
    {
        var admin = userId.HasValue && role == "Admin";
        var team = userId.HasValue && (role is "Admin" or "Staff");
        // Account directories are independent of purchases, requests and enquiries.
        // Project only directory fields; credentials and sessions never leave the API.
        var accounts = admin ? await db.UserAccounts.AsNoTracking()
            .Where(u => u.Role == "Customer" || u.Role == "Staff")
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.UserId)
            .Select(u => new {
                id = u.UserId, name = u.FullName, email = u.Email, phone = u.PhoneNumber,
                role = u.Role, active = u.Active, createdAt = u.CreatedAt,
                orderCount = u.CustomerOrders.Count, requestCount = u.CustomerCakeRequests.Count
            }).ToListAsync() : [];
        var products = await db.Products.AsNoTracking().Include(p => p.ProductImages)
            .Where(p => p.DeletedAt == null && (admin || p.Available)).OrderBy(p => p.ProductId).ToListAsync();
        var requests = userId.HasValue ? await db.CustomCakeRequests.AsNoTracking().Include(r => r.Customer)
            .Include(r => r.Quotations).Include(r => r.CustomCakeImages)
            .Where(r => team || r.CustomerId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync() : [];
        var orders = userId.HasValue ? await db.CustomerOrders.AsNoTracking().Include(o => o.Customer)
            .Include(o => o.Payments).Include(o => o.OrderStatusHistory).Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .Include(o => o.Quotation!).ThenInclude(q => q.CustomCakeRequest)
            .Where(o => team || o.CustomerId == userId).OrderByDescending(o => o.CreatedAt).ToListAsync() : [];
        var enquiries = team ? await db.Enquiries.AsNoTracking().OrderByDescending(e => e.CreatedAt).ToListAsync() : [];
        var reviews = await db.CustomerReviews.AsNoTracking().Where(r => team || r.Status == "Published").OrderByDescending(r => r.CreatedAt).ToListAsync();
        return new
        {
            customers = accounts.Where(u => u.role == "Customer").Select(u => new {
                u.id, u.name, u.email, u.phone, u.active,
                createdAt = DateTime.SpecifyKind(u.createdAt, DateTimeKind.Utc), u.orderCount, u.requestCount
            }),
            staff = accounts.Where(u => u.role == "Staff").Select(u => new {
                u.id, u.name, u.email, u.phone, u.active,
                createdAt = DateTime.SpecifyKind(u.createdAt, DateTimeKind.Utc)
            }),
            products = products.Select(ProductView),
            blocked = await db.BlockedCollectionDates.OrderBy(d => d.Date).Select(d => d.Date).ToListAsync(),
            requests = requests.Select(r =>
            {
                var q = r.Quotations.OrderByDescending(q => q.CreatedAt).ThenByDescending(q => q.QuotationId).FirstOrDefault();
                return new
                {
                    id = r.CustomCakeRequestId,
                    owner = r.Customer.Email,
                    name = r.ContactName.Length > 0 ? r.ContactName : r.Customer.FullName,
                    phone = r.ContactPhone,
                    size = r.CakeSize,
                    flavour = r.Flavour,
                    filling = r.Filling,
                    comments = r.CustomizationDetails,
                    date = Date(r.CollectionDateTime),
                    time = Time(r.CollectionDateTime),
                    status = r.Status == "Quoted" && q?.ExpiresAt <= DateTime.UtcNow ? "Expired" : r.Status,
                    amount = q?.QuotedAmount,
                    notes = q?.Notes,
                    expiresAt = q?.ExpiresAt is DateTime expiry ? DateTime.SpecifyKind(expiry, DateTimeKind.Utc) : (DateTime?)null,
                    image = r.CustomCakeImages.Select(i => new { url = $"/api/CustomCakeImages/{i.CustomCakeImageId}/file" }).FirstOrDefault()
                };
            }),
            orders = orders.Select(o => new
            {
                id = o.OrderId,
                reference = $"KSK-{o.OrderId:D6}",
                owner = o.Customer.Email,
                name = o.ContactName.Length > 0 ? o.ContactName : o.Customer.FullName,
                phone = o.ContactPhone,
                type = o.QuotationId.HasValue ? "Custom" : "Standard",
                total = o.TotalAmount,
                paid = WebsiteRules.Paid(o),
                status = o.OrderStatus,
                date = Date(o.CollectionDateTime),
                time = Time(o.CollectionDateTime),
                slotApproved = o.SlotApproved,
                cancelRequested = o.CancelRequested,
                instructions = o.Instructions,
                items = o.OrderItems.Select(i => new
                {
                    name = i.Product.ProductName,
                    size = i.Product.Size,
                    image = i.Product.Image,
                    quantity = i.Quantity,
                    price = i.UnitPrice
                }).Concat(o.Quotation == null ? [] : new[] {
                        new { name = "Custom cake", size = o.Quotation.CustomCakeRequest.CakeSize, image = "", quantity = 1, price = o.TotalAmount }
                    }),
                payments = o.Payments.Select(p => new
                {
                    id = p.PaymentId,
                    amount = p.Amount,
                    method = p.PaymentMethod,
                    status = p.PaymentStatus == "Completed" ? "Verified" : p.PaymentStatus == "Pending" ? "Awaiting verification" : p.PaymentStatus,
                    reference = p.PaymentReference,
                    proofUrl = p.StoredProofName == null ? null : $"/api/website/payments/{p.PaymentId}/proof"
                }),
                history = o.OrderStatusHistory.OrderBy(h => h.ChangedAt).Select(h => new { status = h.Status, at = DateTime.SpecifyKind(h.ChangedAt, DateTimeKind.Utc) })
            }),
            enquiries = enquiries.Select(e => new { id = e.EnquiryId, name = e.Name, email = e.Email, subject = e.Subject, message = e.Message, status = e.Status }),
            reviews = reviews.Select(r => new { id = r.Id, name = r.Name, rating = r.Rating, message = r.Message, status = r.Status }),
            notifications = Array.Empty<object>()
        };
    }

    public async Task<CustomerOrder> Order(int id, int userId, bool team)
        => await db.CustomerOrders.Include(o => o.Payments).Include(o => o.OrderStatusHistory)
            .FirstOrDefaultAsync(o => o.OrderId == id && (team || o.CustomerId == userId))
            ?? throw new WebsiteException(404, "Order not found.");
    public async Task<CustomCakeRequest> Request(int id, int userId, bool team)
        => await db.CustomCakeRequests.Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.CustomCakeRequestId == id && (team || r.CustomerId == userId))
            ?? throw new WebsiteException(404, "Cake request not found.");
    public Task<List<string>> Blocked() => db.BlockedCollectionDates.Select(d => d.Date).ToListAsync();
    public static void History(CustomerOrder order, string status, int actor) => order.OrderStatusHistory.Add(new OrderStatusHistory
    {
        Status = status,
        Notes = $"Changed by account {actor}.",
        ChangedAt = DateTime.UtcNow
    });
}

using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<CustomCakeRequest> CustomCakeRequests { get; set; }
        public DbSet<CustomCakeImage> CustomCakeImages { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<CustomerOrder> CustomerOrders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
        public DbSet<Enquiry> Enquiries { get; set; }
        public DbSet<CustomerNotification> CustomerNotifications { get; set; } = null!;


        public DbSet<BlockedCollectionDate> BlockedCollectionDates { get; set; }
        public DbSet<CustomerReview> CustomerReviews { get; set; }
        public DbSet<WebsiteSubmission> WebsiteSubmissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<WebsiteSubmission>().HasKey(s => new { s.CustomerId, s.Kind, s.Id });
            modelBuilder.Entity<CustomerOrder>().HasIndex(o => o.QuotationId)
                .IsUnique().HasFilter("[QuotationId] IS NOT NULL");
            modelBuilder.Entity<CustomerOrder>().HasIndex(o => o.RequestId)
                .IsUnique().HasFilter("[RequestId] IS NOT NULL");
            modelBuilder.Entity<BlockedCollectionDate>().HasIndex(d => d.Date).IsUnique();
            modelBuilder.Entity<Payment>().HasIndex(p => p.PaymentReference)
                .IsUnique().HasFilter("[PaymentReference] IS NOT NULL");

            modelBuilder.Entity<UserAccount>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<UserAccount>()
                .Property(u => u.Role)
                .HasDefaultValue("Customer");

            modelBuilder.Entity<UserSession>()
                .HasOne(s => s.UserAccount)
                .WithMany(u => u.UserSessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.ProductImages)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomCakeRequest>()
                .HasOne(r => r.Customer)
                .WithMany(u => u.CustomerCakeRequests)
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomCakeRequest>()
                .HasOne(r => r.Consultant)
                .WithMany(u => u.ConsultantCakeRequests)
                .HasForeignKey(r => r.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomCakeImage>()
                .HasOne(i => i.CustomCakeRequest)
                .WithMany(r => r.CustomCakeImages)
                .HasForeignKey(i => i.CustomCakeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.CustomCakeRequest)
                .WithMany(r => r.Quotations)
                .HasForeignKey(q => q.CustomCakeRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.Consultant)
                .WithMany(u => u.Quotations)
                .HasForeignKey(q => q.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerOrder>()
                .HasOne(o => o.Customer)
                .WithMany(u => u.CustomerOrders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerOrder>()
                .HasOne(o => o.Quotation)
                .WithMany()
                .HasForeignKey(o => o.QuotationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.CustomerOrder)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.CustomerOrder)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(h => h.CustomerOrder)
                .WithMany(o => o.OrderStatusHistory)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enquiry>()
                .HasOne(e => e.Customer)
                .WithMany(u => u.Enquiries)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);


            modelBuilder.Entity<CustomerNotification>(entity =>
            {
                entity.ToTable("CustomerNotifications");

                entity.HasKey(n => n.NotificationId);

                entity.Property(n => n.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(n => n.Message)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(n => n.IsRead)
                    .HasDefaultValue(false);


                entity.HasOne(n => n.Customer)
                    .WithMany(u => u.CustomerNotifications)
                    .HasForeignKey(n => n.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.CustomerOrder)
                    .WithMany()
                    .HasForeignKey(n => n.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);

            });

        }
    }
}

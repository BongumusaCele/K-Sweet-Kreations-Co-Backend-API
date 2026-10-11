
using K_Sweet_Kreations_Co_Backend_API.Controllers;
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class PaymentVerificationTests
    {
        // Create an isolated SQLite database for each test.
        private static async Task<(
            ApplicationDbContext Context,
            SqliteConnection Connection)> CreateDatabase()
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options;

            var context = new ApplicationDbContext(options);

            // SQLite-compatible decimal mapping for test calculations.
            foreach (var property in context.Model
                .GetEntityTypes()
                .SelectMany(e => e.GetProperties())
                .Where(p => p.ClrType == typeof(decimal)))
            {
                // Model metadata is finalized at this point.
                // Decimal conversions should be configured in
                // OnModelCreating, not changed here.
            }

            await context.Database.EnsureCreatedAsync();

            return (context, connection);
        }

        // Simulate an authenticated Admin.
        private static PaymentsController CreateAdminController(
            ApplicationDbContext context)
        {
            var controller = new PaymentsController(context);

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier, "1"),

                new Claim(
                    ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(
                claims, "TestAuthentication");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return controller;
        }

        // Create a customer and order using the actual
        // UserAccount and CustomerOrder model properties.
        private static async Task<CustomerOrder> CreateOrder(
            ApplicationDbContext context,
            decimal totalAmount = 1000m,
            string status = "Pending Deposit")
        {
            var customer = new UserAccount
            {
                FullName = "Test Customer",

                Email =
                    $"customer-{Guid.NewGuid():N}@test.com",

                PhoneNumber = "0712345678",

                PasswordHash = "TestPasswordHash",

                Role = "Customer",

                Active = true,

                CreatedAt = DateTime.UtcNow
            };

            context.UserAccounts.Add(customer);

            await context.SaveChangesAsync();

            var order = new CustomerOrder
            {
                CustomerId = customer.UserId,

                TotalAmount = totalAmount,

                OrderStatus = status,

                CollectionDateTime =
                    DateTime.UtcNow.AddDays(7),

                CreatedAt = DateTime.UtcNow
            };

            context.CustomerOrders.Add(order);

            await context.SaveChangesAsync();

            return order;
        }

        // Create a payment awaiting admin verification.
        private static async Task<Payment> CreatePendingPayment(
            ApplicationDbContext context,
            int orderId,
            decimal amount)
        {
            var payment = new Payment
            {
                OrderId = orderId,

                Amount = amount,

                PaymentMethod = "EFT",

                PaymentStatus = "Pending",

                PaymentReference =
                    $"TEST-{Guid.NewGuid():N}",

                CreatedAt = DateTime.UtcNow
            };

            context.Payments.Add(payment);

            await context.SaveChangesAsync();

            return payment;
        }

        // TEST 1: A verified 50% deposit updates the order.
        [Fact]
        public async Task VerifyPayment_HalfDeposit_UpdatesOrderToDepositPaid()
        {
            var (context, connection) =
                await CreateDatabase();

            await using (connection)
            await using (context)
            {
                var order = await CreateOrder(context);

                var payment = await CreatePendingPayment(
                    context,
                    order.OrderId,
                    500m);

                var controller =
                    CreateAdminController(context);

                var dto = new VerifyPaymentDto
                {
                    Status = "Completed"
                };

                var result = await controller.VerifyPayment(
                    payment.PaymentId,
                    dto);

                Assert.IsType<OkObjectResult>(result);

                await context.Entry(order).ReloadAsync();
                await context.Entry(payment).ReloadAsync();

                Assert.Equal(
                    "Deposit Paid",
                    order.OrderStatus);

                Assert.Equal(
                    "Completed",
                    payment.PaymentStatus);

                Assert.NotNull(payment.PaidAt);
            }
        }

        // TEST 2: A fully paid order changes to Paid.
        [Fact]
        public async Task VerifyPayment_FullPayment_UpdatesOrderToPaid()
        {
            var (context, connection) =
                await CreateDatabase();

            await using (connection)
            await using (context)
            {
                var order = await CreateOrder(context);

                var firstPayment = new Payment
                {
                    OrderId = order.OrderId,

                    Amount = 500m,

                    PaymentMethod = "EFT",

                    PaymentStatus = "Completed",

                    PaidAt = DateTime.UtcNow
                };

                context.Payments.Add(firstPayment);

                await context.SaveChangesAsync();

                var finalPayment = await CreatePendingPayment(
                    context,
                    order.OrderId,
                    500m);

                var controller =
                    CreateAdminController(context);

                var dto = new VerifyPaymentDto
                {
                    Status = "Completed"
                };

                var result = await controller.VerifyPayment(
                    finalPayment.PaymentId,
                    dto);

                Assert.IsType<OkObjectResult>(result);

                await context.Entry(order).ReloadAsync();
                await context.Entry(finalPayment).ReloadAsync();

                Assert.Equal("Paid", order.OrderStatus);

                Assert.Equal(
                    "Completed",
                    finalPayment.PaymentStatus);
            }
        }

        // TEST 3: Rejected payments remain unconfirmed.
        [Fact]
        public async Task VerifyPayment_Rejected_UpdatesPaymentStatus()
        {
            var (context, connection) =
                await CreateDatabase();

            await using (connection)
            await using (context)
            {
                var order = await CreateOrder(context);

                var payment = await CreatePendingPayment(
                    context,
                    order.OrderId,
                    500m);

                var controller =
                    CreateAdminController(context);

                var dto = new VerifyPaymentDto
                {
                    Status = "Rejected"
                };

                var result = await controller.VerifyPayment(
                    payment.PaymentId,
                    dto);

                Assert.IsType<OkObjectResult>(result);

                await context.Entry(payment).ReloadAsync();
                await context.Entry(order).ReloadAsync();

                Assert.Equal(
                    "Rejected",
                    payment.PaymentStatus);

                Assert.Null(payment.PaidAt);

                Assert.Equal(
                    "Pending Deposit",
                    order.OrderStatus);
            }
        }

        // TEST 4: An already reviewed payment cannot
        // be verified again.
        [Fact]
        public async Task VerifyPayment_AlreadyReviewed_ReturnsConflict()
        {
            var (context, connection) =
                await CreateDatabase();

            await using (connection)
            await using (context)
            {
                var order = await CreateOrder(context);

                var payment = await CreatePendingPayment(
                    context,
                    order.OrderId,
                    500m);

                payment.PaymentStatus = "Completed";
                payment.PaidAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                var controller =
                    CreateAdminController(context);

                var dto = new VerifyPaymentDto
                {
                    Status = "Completed"
                };

                var result = await controller.VerifyPayment(
                    payment.PaymentId,
                    dto);

                Assert.IsType<ConflictObjectResult>(result);
            }
        }

        // TEST 5: Payment below 50% does not mark
        // the deposit as fully paid.
        [Fact]
        public async Task VerifyPayment_BelowHalfDeposit_KeepsPendingStatus()
        {
            var (context, connection) =
                await CreateDatabase();

            await using (connection)
            await using (context)
            {
                var order = await CreateOrder(context);

                var payment = await CreatePendingPayment(
                    context,
                    order.OrderId,
                    400m);

                var controller =
                    CreateAdminController(context);

                var dto = new VerifyPaymentDto
                {
                    Status = "Completed"
                };

                var result = await controller.VerifyPayment(
                    payment.PaymentId,
                    dto);

                Assert.IsType<OkObjectResult>(result);

                await context.Entry(order).ReloadAsync();
                await context.Entry(payment).ReloadAsync();

                Assert.Equal(
                    "Pending Deposit",
                    order.OrderStatus);

                Assert.Equal(
                    "Completed",
                    payment.PaymentStatus);
            }
        }
    }
}


using K_Sweet_Kreations_Co_Backend_API.Controllers;
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Observers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class PaymentTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;

            return new ApplicationDbContext(options);
        }

        private static PaymentsController CreateController(
            ApplicationDbContext context,
            int customerId)
        {
            var observers = new List<IOrderObserver>
{
    new CustomerNotificationObserver(context)
};

            var orderSubject = new OrderSubject(observers);

            var controller = new PaymentsController(
                context,
                orderSubject);

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    customerId.ToString()),
                new Claim(ClaimTypes.Role, "Customer")
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            claims, "TestAuthentication"))
                }
            };

            return controller;
        }

        private static async Task<CustomerOrder> CreateOrder(
            ApplicationDbContext context,
            int customerId = 1,
            decimal totalAmount = 1000m,
            string status = "Pending Deposit")
        {
            var order = new CustomerOrder
            {
                CustomerId = customerId,
                TotalAmount = totalAmount,
                OrderStatus = status,
                CollectionDateTime =
                    DateTime.UtcNow.AddDays(7)
            };

            context.CustomerOrders.Add(order);
            await context.SaveChangesAsync();

            return order;
        }

        [Fact]
        public async Task CreatePayment_ValidPayment_ReturnsCreated()
        {
            using var context = CreateContext();
            var order = await CreateOrder(context);
            var controller = CreateController(context, 1);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 500m,
                PaymentMethod = "EFT",
                PaymentReference = "TEST-001"
            };

            var result = await controller.CreatePayment(dto);

            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, created.StatusCode);

            var payment = await context.Payments.SingleAsync();
            Assert.Equal("Pending", payment.PaymentStatus);
            Assert.Equal(500m, payment.Amount);
        }

        [Fact]
        public async Task CreatePayment_OtherCustomer_ReturnsNotFound()
        {
            using var context = CreateContext();
            var order = await CreateOrder(context, customerId: 1);
            var controller = CreateController(context, 2);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 500m,
                PaymentMethod = "EFT"
            };

            var result = await controller.CreatePayment(dto);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreatePayment_CancelledOrder_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var order = await CreateOrder(
                context, status: "Cancelled");

            var controller = CreateController(context, 1);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 500m,
                PaymentMethod = "EFT"
            };

            var result = await controller.CreatePayment(dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePayment_ZeroAmount_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var order = await CreateOrder(context);
            var controller = CreateController(context, 1);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 0m,
                PaymentMethod = "EFT"
            };

            var result = await controller.CreatePayment(dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePayment_InvalidMethod_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var order = await CreateOrder(context);
            var controller = CreateController(context, 1);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 500m,
                PaymentMethod = "Cash"
            };

            var result = await controller.CreatePayment(dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePayment_ExceedsBalance_ReturnsBadRequest()
        {
            using var context = CreateContext();
            var order = await CreateOrder(context);
            var controller = CreateController(context, 1);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 1200m,
                PaymentMethod = "EFT"
            };

            var result = await controller.CreatePayment(dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePayment_DuplicateReference_ReturnsConflict()
        {
            using var context = CreateContext();
            var order = await CreateOrder(context);

            context.Payments.Add(new Payment
            {
                OrderId = order.OrderId,
                Amount = 200m,
                PaymentMethod = "EFT",
                PaymentStatus = "Pending",
                PaymentReference = "DUPLICATE-001"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, 1);

            var dto = new CreatePaymentDto
            {
                OrderId = order.OrderId,
                Amount = 300m,
                PaymentMethod = "EFT",
                PaymentReference = "DUPLICATE-001"
            };

            var result = await controller.CreatePayment(dto);

            Assert.IsType<ConflictObjectResult>(result);
        }
    }
}


using K_Sweet_Kreations_Co_Backend_API.Controllers;
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class QuotationTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;

            return new ApplicationDbContext(options);
        }

        private static QuotationsController CreateController(
            ApplicationDbContext context,
            int userId,
            string role)
        {
            var controller = new QuotationsController(context);

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.ToString()),

                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(
                claims, "TestAuthentication");

            controller.ControllerContext =
                new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity)
                    }
                };

            return controller;
        }

        private static CustomCakeRequest CreateCakeRequest(
            int customerId,
            string status = "Under Review")
        {
            return new CustomCakeRequest
            {
                CustomerId = customerId,
                CakeSize = "Medium",
                Flavour = "Chocolate",
                CollectionDateTime =
                    DateTime.UtcNow.AddDays(7),
                Status = status
            };
        }

        [Fact]
        public async Task GetQuotation_OtherCustomer_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();

            var request = CreateCakeRequest(1);

            context.CustomCakeRequests.Add(request);
            await context.SaveChangesAsync();

            var quotation = new Quotation
            {
                CustomCakeRequestId =
                    request.CustomCakeRequestId,
                ConsultantId = 3,
                QuotedAmount = 850m,
                Status = "Pending"
            };

            context.Quotations.Add(quotation);
            await context.SaveChangesAsync();

            var controller = CreateController(
                context, 2, "Customer");

            // Act
            var result = await controller.GetQuotation(
                quotation.QuotationId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetQuotation_Owner_ReturnsOk()
        {
            // Arrange
            using var context = CreateContext();

            var request = CreateCakeRequest(1);

            context.CustomCakeRequests.Add(request);
            await context.SaveChangesAsync();

            var quotation = new Quotation
            {
                CustomCakeRequestId =
                    request.CustomCakeRequestId,
                ConsultantId = 3,
                QuotedAmount = 850m,
                Status = "Pending"
            };

            context.Quotations.Add(quotation);
            await context.SaveChangesAsync();

            var controller = CreateController(
                context, 1, "Customer");

            // Act
            var result = await controller.GetQuotation(
                quotation.QuotationId);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RespondToQuotation_AlreadyAccepted_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var request = CreateCakeRequest(
                1, "Quote Accepted");

            context.CustomCakeRequests.Add(request);
            await context.SaveChangesAsync();

            var quotation = new Quotation
            {
                CustomCakeRequestId =
                    request.CustomCakeRequestId,
                ConsultantId = 3,
                QuotedAmount = 850m,
                Status = "Accepted"
            };

            context.Quotations.Add(quotation);
            await context.SaveChangesAsync();

            var controller = CreateController(
                context, 1, "Customer");

            var dto = new RespondToQuotationDto
            {
                Status = "Accepted"
            };

            // Act
            var result =
                await controller.RespondToQuotation(
                    quotation.QuotationId, dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RespondToQuotation_Expired_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var request = CreateCakeRequest(1);

            context.CustomCakeRequests.Add(request);
            await context.SaveChangesAsync();

            var quotation = new Quotation
            {
                CustomCakeRequestId =
                    request.CustomCakeRequestId,
                ConsultantId = 3,
                QuotedAmount = 850m,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            };

            context.Quotations.Add(quotation);
            await context.SaveChangesAsync();

            var controller = CreateController(
                context, 1, "Customer");

            var dto = new RespondToQuotationDto
            {
                Status = "Accepted"
            };

            // Act
            var result =
                await controller.RespondToQuotation(
                    quotation.QuotationId, dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}

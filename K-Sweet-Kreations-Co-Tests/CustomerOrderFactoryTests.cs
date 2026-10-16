
using System;
using K_Sweet_Kreations_Co_Backend_API.Factories;
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class CustomerOrderFactoryTests
    {
        private readonly CustomerOrderFactory _factory = new();

        [Fact]
        public void CreateFromQuotation_ShouldCreateOrderWithCorrectDetails()
        {
            // Arrange
            int customerId = 1;
            int quotationId = 6;
            decimal totalAmount = 800m;
            DateTime collectionDate = DateTime.UtcNow.AddDays(3);

            // Act
            var order = _factory.CreateFromQuotation(
                customerId,
                quotationId,
                totalAmount,
                collectionDate);

            // Assert
            Assert.NotNull(order);
            Assert.Equal(customerId, order.CustomerId);
            Assert.Equal(quotationId, order.QuotationId);
            Assert.Equal(totalAmount, order.TotalAmount);
            Assert.Equal(collectionDate, order.CollectionDateTime);
        }

        [Fact]
        public void CreateFromQuotation_ShouldSetPendingDepositStatus()
        {
            // Arrange
            DateTime collectionDate = DateTime.UtcNow.AddDays(3);

            // Act
            var order = _factory.CreateFromQuotation(
                1, 6, 800m, collectionDate);

            // Assert
            Assert.Equal("Pending Deposit", order.OrderStatus);
        }

        [Fact]
        public void CreateFromQuotation_ShouldSetCreationDate()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var order = _factory.CreateFromQuotation(
                1, 6, 800m,
                DateTime.UtcNow.AddDays(3));

            var afterCreation = DateTime.UtcNow;

            // Assert
            Assert.InRange(
                order.CreatedAt,
                beforeCreation,
                afterCreation);
        }
    }
}

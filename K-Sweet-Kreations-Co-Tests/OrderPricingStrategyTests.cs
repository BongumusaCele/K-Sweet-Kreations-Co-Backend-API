
using K_Sweet_Kreations_Co_Backend_API.Factories;
using K_Sweet_Kreations_Co_Backend_API.Services;
using K_Sweet_Kreations_Co_Backend_API.Strategies;
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class OrderPricingStrategyTests
    {
        [Fact]
        public void CalculateTotal_ShouldReturnAcceptedQuotationAmount()
        {
            // Arrange
            IOrderPricingStrategy strategy =
                new AcceptedQuotationPricingStrategy();

            decimal quotedAmount = 800m;

            // Act
            decimal result = strategy.CalculateTotal(quotedAmount);

            // Assert
            Assert.Equal(800m, result);
        }

        [Theory]
        [InlineData(350, 350)]
        [InlineData(600, 600)]
        [InlineData(800, 800)]
        [InlineData(1500, 1500)]
        public void CalculateTotal_ShouldPreserveQuotationAmount(
            decimal quotedAmount,
            decimal expectedTotal)
        {
            // Arrange
            IOrderPricingStrategy strategy =
                new AcceptedQuotationPricingStrategy();

            // Act
            decimal result = strategy.CalculateTotal(quotedAmount);

            // Assert
            Assert.Equal(expectedTotal, result);
        }


        [Fact]
        public void OrderService_ShouldUseInjectedPricingStrategy()
        {
            // Arrange
            IOrderPricingStrategy strategy =
                new AcceptedQuotationPricingStrategy();

            ICustomerOrderFactory orderFactory =
                new CustomerOrderFactory();

            var service = new OrderService(strategy, orderFactory);

            // Act
            decimal total = service.CalculateOrderTotal(600m);

            // Assert
            Assert.Equal(600m, total);
        }

    }
}

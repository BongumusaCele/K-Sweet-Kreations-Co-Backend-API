
using K_Sweet_Kreations_Co_Backend_API.Factories;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Strategies;

namespace K_Sweet_Kreations_Co_Backend_API.Services
{
    public class OrderService
    {
        private readonly IOrderPricingStrategy _pricingStrategy;
        private readonly ICustomerOrderFactory _orderFactory;

        public OrderService(
            IOrderPricingStrategy pricingStrategy,
            ICustomerOrderFactory orderFactory)
        {
            _pricingStrategy = pricingStrategy;
            _orderFactory = orderFactory;
        }

        // Strategy Pattern: calculates the order total.
        public decimal CalculateOrderTotal(decimal quotedAmount)
        {
            return _pricingStrategy.CalculateTotal(quotedAmount);
        }

        // Factory Pattern: creates an order using the calculated price.
        public CustomerOrder CreateOrderFromQuotation(
            int customerId,
            int quotationId,
            decimal quotedAmount,
            DateTime collectionDateTime)
        {
            decimal totalAmount =
                CalculateOrderTotal(quotedAmount);

            return _orderFactory.CreateFromQuotation(
                customerId,
                quotationId,
                totalAmount,
                collectionDateTime);
        }
    }
}

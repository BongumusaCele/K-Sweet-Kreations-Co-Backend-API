using K_Sweet_Kreations_Co_Backend_API.Models;

namespace K_Sweet_Kreations_Co_Backend_API.Factories
{
    public class CustomerOrderFactory : ICustomerOrderFactory
    {
        public CustomerOrder CreateFromQuotation(
            int customerId,
            int quotationId,
            decimal totalAmount,
            DateTime collectionDateTime)
        {
            return new CustomerOrder
            {
                CustomerId = customerId,
                QuotationId = quotationId,
                TotalAmount = totalAmount,
                CollectionDateTime = collectionDateTime,
                OrderStatus = "Pending Deposit",
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
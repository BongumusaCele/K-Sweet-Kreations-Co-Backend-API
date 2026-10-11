using K_Sweet_Kreations_Co_Backend_API.Models;

namespace K_Sweet_Kreations_Co_Backend_API.Factories
{
    public interface ICustomerOrderFactory
    {
        CustomerOrder CreateFromQuotation(
            int customerId,
            int quotationId,
            decimal totalAmount,
            DateTime collectionDateTime);
    }
}
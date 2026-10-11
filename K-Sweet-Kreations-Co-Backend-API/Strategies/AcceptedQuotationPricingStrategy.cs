using System;

namespace K_Sweet_Kreations_Co_Backend_API.Strategies
{
    public class AcceptedQuotationPricingStrategy
        : IOrderPricingStrategy
    {
        public decimal CalculateTotal(decimal quotedAmount)
        {
            if (quotedAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quotedAmount),
                    "The quotation amount must be greater than zero.");
            }

            return quotedAmount;
        }
    }
}
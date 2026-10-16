namespace K_Sweet_Kreations_Co_Backend_API.Strategies
{
    public interface IOrderPricingStrategy
    {
        decimal CalculateTotal(decimal quotedAmount);
    }
}
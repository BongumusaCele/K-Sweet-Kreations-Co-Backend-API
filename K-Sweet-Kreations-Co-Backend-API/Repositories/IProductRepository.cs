
using K_Sweet_Kreations_Co_Backend_API.Models;

namespace K_Sweet_Kreations_Co_Backend_API.Repositories
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync();

        Task<Product?> GetByIdAsync(int id);

        Task AddAsync(Product product);

        Task UpdateAsync(Product product);

        Task DeleteAsync(Product product);
    }
}


using K_Sweet_Kreations_Co_Backend_API.Dtos;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Repositories;

namespace K_Sweet_Kreations_Co_Backend_API.Services
{
    public class ProductService
    {
        private readonly IProductRepository _repository;

        public ProductService(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ProductResponseDto>> GetAllAsync()
        {
            var products = await _repository.GetAllAsync();

            return products.Select(MapToResponse).ToList();
        }

        public async Task<ProductResponseDto?> GetByIdAsync(int id)
        {
            var product = await _repository.GetByIdAsync(id);

            return product == null
                ? null
                : MapToResponse(product);
        }

        public async Task<ProductResponseDto> CreateAsync(
            CreateProductDto dto)
        {
            var product = new Product
            {
                ProductName = dto.ProductName,
                Description = dto.Description,
                Category = dto.Category,
                StartingPrice = dto.StartingPrice,
                Available = dto.Available,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(product);

            return MapToResponse(product);
        }

        public async Task<bool> UpdateAsync(
            int id,
            UpdateProductDto dto)
        {
            var product = await _repository.GetByIdAsync(id);

            if (product == null)
            {
                return false;
            }

            product.ProductName = dto.ProductName;
            product.Description = dto.Description;
            product.Category = dto.Category;
            product.StartingPrice = dto.StartingPrice;
            product.Available = dto.Available;

            await _repository.UpdateAsync(product);

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var product = await _repository.GetByIdAsync(id);

            if (product == null)
            {
                return false;
            }

            await _repository.DeleteAsync(product);

            return true;
        }

        private static ProductResponseDto MapToResponse(
            Product product)
        {
            return new ProductResponseDto
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Description = product.Description,
                Category = product.Category,
                StartingPrice = product.StartingPrice,
                Available = product.Available,
                CreatedAt = product.CreatedAt
            };
        }
    }
}


using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using K_Sweet_Kreations_Co_Backend_API.Services;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductImagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PrivateImageStore _images;


        public ProductImagesController(
            ApplicationDbContext context,
            PrivateImageStore images)
        {
            _context = context;
            _images = images;

        }

        // POST: api/ProductImages/upload
        [HttpPost("upload")]
        [RequestSizeLimit(3 * 1024 * 1024)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage(
    [FromForm] int productId,
    IFormFile file,
    [FromForm] bool isPrimary = false,
    [FromForm] bool isGalleryImage = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new
                {
                    message = "Please select an image."
                });

            var product = await _context.Products
                .FindAsync(productId);

            if (product == null || product.DeletedAt != null)
                return NotFound(new
                {
                    message = "Product not found."
                });

            var fileName = await _images.Save(file, "products");
            var imageUrl = $"/uploads/products/{fileName}";
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = imageUrl,
                IsPrimary = isPrimary,
                IsGalleryImage = isGalleryImage
            };

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                if (isPrimary)
                {
                    var previousPrimaryImages =
                        await _context.ProductImages
                            .Where(i => i.ProductId == productId &&
                                        i.IsPrimary)
                            .ToListAsync();

                    product.Image = imageUrl;
                    foreach (var image in previousPrimaryImages)
                        image.IsPrimary = false;
                }

                _context.ProductImages.Add(productImage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await _images.Delete("products", fileName);

                throw;
            }

            return CreatedAtAction(
                nameof(GetImageById),
                new { id = productImage.ProductImageId },
                new
                {
                    message = "Product image uploaded successfully.",
                    productImage.ProductImageId,
                    productImage.ProductId,
                    productImage.ImageUrl,
                    productImage.IsPrimary,
                    productImage.IsGalleryImage
                });
        }

        // GET: api/ProductImages/1
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetImageById(int id)
        {
            var image = await _context.ProductImages
                .AsNoTracking()
                .Where(i => i.ProductImageId == id && i.Product.DeletedAt == null)
                .Select(i => new
                {
                    i.ProductImageId,
                    i.ProductId,
                    i.ImageUrl,
                    i.IsPrimary,
                    i.IsGalleryImage
                })
                .FirstOrDefaultAsync();

            if (image == null)
                return NotFound(new
                {
                    message = "Product image not found."
                });

            return Ok(image);
        }

        // GET: api/ProductImages/product/4
        [HttpGet("product/{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductImages(int productId)
        {
            var productExists = await _context.Products
                .AnyAsync(p => p.ProductId == productId && p.DeletedAt == null);

            if (!productExists)
                return NotFound(new
                {
                    message = "Product not found."
                });

            var images = await _context.ProductImages
                .AsNoTracking()
                .Where(i => i.ProductId == productId)
                .OrderByDescending(i => i.IsPrimary)
                .Select(i => new
                {
                    i.ProductImageId,
                    i.ProductId,
                    i.ImageUrl,
                    i.IsPrimary,
                    i.IsGalleryImage
                })
                .ToListAsync();

            return Ok(images);
        }
    }
}

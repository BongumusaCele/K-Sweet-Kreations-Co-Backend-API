
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductImagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductImagesController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // POST: api/ProductImages/upload
        [HttpPost("upload")]
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

            if (product == null)
                return NotFound(new
                {
                    message = "Product not found."
                });

            // Maximum image size: 5 MB
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new
                {
                    message = "Image cannot exceed 5 MB."
                });

            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new
                {
                    message = "Only JPG, PNG and WebP images are allowed."
                });

            // Validate image file signatures
            var header = new byte[12];
            await using (var input = file.OpenReadStream())
            {
                var bytesRead = await input.ReadAsync(header);

                bool validJpeg = bytesRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF;

                bool validPng = bytesRead >= 8 &&
                    header.Take(8).SequenceEqual(
                        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

                bool validWebp = bytesRead >= 12 &&
                    System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                    System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";

                bool validImage = extension switch
                {
                    ".jpg" or ".jpeg" => validJpeg,
                    ".png" => validPng,
                    ".webp" => validWebp,
                    _ => false
                };

                if (!validImage)
                    return BadRequest(new
                    {
                        message = "Invalid image file format."
                    });
            }

            var uploadsFolder = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot", "uploads", "products");

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(
                filePath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

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

                    foreach (var image in previousPrimaryImages)
                        image.IsPrimary = false;
                }

                _context.ProductImages.Add(productImage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

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
                .Where(i => i.ProductImageId == id)
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
                .AnyAsync(p => p.ProductId == productId);

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

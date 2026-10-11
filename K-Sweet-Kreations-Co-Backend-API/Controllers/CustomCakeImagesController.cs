
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomCakeImagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CustomCakeImagesController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Get authenticated user ID
        private bool TryGetUserId(out int userId)
        {
            var value = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(value, out userId);
        }

        // Check whether user is authorized staff
        private bool IsStaff()
        {
            return User.IsInRole("Admin") ||
                   User.IsInRole("Consultant");
        }

        // Private storage location
        private string GetPrivateUploadsFolder()
        {
            return Path.Combine(
                _environment.ContentRootPath,
                "PrivateUploads",
                "custom-cakes");
        }

        // Determine image content type
        private static string GetImageContentType(
            string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        // POST: api/CustomCakeImages/upload
        [HttpPost("upload")]
        [Authorize(Roles = "Customer")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage(
            [FromForm] int customCakeRequestId,
            IFormFile file)
        {
            if (!TryGetUserId(out int customerId))
            {
                return Unauthorized(new
                {
                    message = "Invalid customer authentication."
                });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select an inspiration image."
                });
            }

            // Verify that the request belongs to the customer
            var request = await _context.CustomCakeRequests
                .FirstOrDefaultAsync(r =>
                    r.CustomCakeRequestId == customCakeRequestId &&
                    r.CustomerId == customerId);

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Custom cake request not found."
                });
            }

            // Only allow uploads while request is editable
            if (request.Status != "Pending" &&
                request.Status != "Under Review")
            {
                return BadRequest(new
                {
                    message = "Images cannot be added to this request at its current status."
                });
            }

            // Maximum file size: 5 MB
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new
                {
                    message = "Image cannot exceed 5 MB."
                });
            }

            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    message = "Only JPG, PNG and WebP images are allowed."
                });
            }

            // Validate actual file signature
            var header = new byte[12];

            await using (var input = file.OpenReadStream())
            {
                int bytesRead = 0;

                while (bytesRead < header.Length)
                {
                    int read = await input.ReadAsync(
                        header.AsMemory(
                            bytesRead,
                            header.Length - bytesRead));

                    if (read == 0)
                        break;

                    bytesRead += read;
                }

                bool validJpeg = bytesRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF;

                bool validPng = bytesRead >= 8 &&
                    header.Take(8).SequenceEqual(
                        new byte[]
                        {
                            137, 80, 78, 71, 13, 10, 26, 10
                        });

                bool validWebp = bytesRead >= 12 &&
                    Encoding.ASCII.GetString(
                        header, 0, 4) == "RIFF" &&
                    Encoding.ASCII.GetString(
                        header, 8, 4) == "WEBP";

                bool validImage = extension switch
                {
                    ".jpg" or ".jpeg" => validJpeg,
                    ".png" => validPng,
                    ".webp" => validWebp,
                    _ => false
                };

                if (!validImage)
                {
                    return BadRequest(new
                    {
                        message = "Invalid image file format."
                    });
                }
            }

            // Store files outside wwwroot
            var uploadsFolder = GetPrivateUploadsFolder();

            Directory.CreateDirectory(uploadsFolder);

            // Generate secure unique filename
            var fileName = $"{Guid.NewGuid():N}{extension}";

            var filePath = Path.Combine(
                uploadsFolder,
                fileName);

            await using (var stream = new FileStream(
                filePath,
                FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            // Create database image record
            var image = new CustomCakeImage
            {
                CustomCakeRequestId = customCakeRequestId,
                StoredFileName = fileName,
                UploadedAt = DateTime.UtcNow,

                // Temporary required value until ID is generated
                ImageUrl = string.Empty
            };

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                _context.CustomCakeImages.Add(image);

                // Generate image ID
                await _context.SaveChangesAsync();

                // Generate authenticated image URL
                image.ImageUrl =
                    $"/api/CustomCakeImages/{image.CustomCakeImageId}/file";

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                throw;
            }

            return CreatedAtAction(
                nameof(GetImageById),
                new { id = image.CustomCakeImageId },
                new
                {
                    message = "Inspiration image uploaded successfully.",
                    image.CustomCakeImageId,
                    image.CustomCakeRequestId,
                    image.ImageUrl,
                    image.UploadedAt
                });
        }

        // GET: api/CustomCakeImages/1
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Customer,Admin,Consultant")]
        public async Task<IActionResult> GetImageById(int id)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            bool isStaff = IsStaff();

            var image = await _context.CustomCakeImages
                .AsNoTracking()
                .Where(i => i.CustomCakeImageId == id)
                .Where(i => isStaff ||
                    i.CustomCakeRequest.CustomerId == userId)
                .Select(i => new
                {
                    i.CustomCakeImageId,
                    i.CustomCakeRequestId,
                    i.ImageUrl,
                    i.UploadedAt
                })
                .FirstOrDefaultAsync();

            if (image == null)
            {
                return NotFound(new
                {
                    message = "Inspiration image not found."
                });
            }

            return Ok(image);
        }

        // GET: api/CustomCakeImages/request/5
        [HttpGet("request/{requestId:int}")]
        [Authorize(Roles = "Customer,Admin,Consultant")]
        public async Task<IActionResult> GetRequestImages(
            int requestId)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            bool isStaff = IsStaff();

            var requestExists =
                await _context.CustomCakeRequests
                    .AnyAsync(r =>
                        r.CustomCakeRequestId == requestId &&
                        (isStaff || r.CustomerId == userId));

            if (!requestExists)
            {
                return NotFound(new
                {
                    message = "Custom cake request not found."
                });
            }

            var images = await _context.CustomCakeImages
                .AsNoTracking()
                .Where(i =>
                    i.CustomCakeRequestId == requestId)
                .OrderByDescending(i => i.UploadedAt)
                .Select(i => new
                {
                    i.CustomCakeImageId,
                    i.CustomCakeRequestId,
                    i.ImageUrl,
                    i.UploadedAt
                })
                .ToListAsync();

            return Ok(images);
        }

        // GET: api/CustomCakeImages/1/file
        // Secure image retrieval
        [HttpGet("{id:int}/file")]
        [Authorize(Roles = "Customer,Admin,Consultant")]
        public async Task<IActionResult> GetImageFile(int id)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            bool isStaff = IsStaff();

            // Check ownership before accessing file
            var image = await _context.CustomCakeImages
                .AsNoTracking()
                .Where(i => i.CustomCakeImageId == id)
                .Where(i => isStaff ||
                    i.CustomCakeRequest.CustomerId == userId)
                .FirstOrDefaultAsync();

            if (image == null)
            {
                return NotFound(new
                {
                    message = "Inspiration image not found."
                });
            }

            // Retrieve actual filename from database
            string fileName = image.StoredFileName
                ?? string.Empty;

            // Validate stored filename
            string extension = Path.GetExtension(fileName)
                .ToLowerInvariant();

            string nameWithoutExtension =
                Path.GetFileNameWithoutExtension(fileName);

            if (!Guid.TryParseExact(
                    nameWithoutExtension, "N", out _) ||
                !new[]
                {
                    ".jpg", ".jpeg", ".png", ".webp"
                }.Contains(extension))
            {
                return NotFound(new
                {
                    message = "Image file not found."
                });
            }

            // Build private physical file path
            var filePath = Path.Combine(
                GetPrivateUploadsFolder(),
                fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new
                {
                    message = "Image file not found."
                });
            }

            var contentType =
                GetImageContentType(extension);

            // Prevent caching of private images
            Response.Headers.CacheControl =
                "private, no-store";

            Response.Headers["X-Content-Type-Options"] =
                "nosniff";

            return PhysicalFile(filePath, contentType);
        }
    }
}

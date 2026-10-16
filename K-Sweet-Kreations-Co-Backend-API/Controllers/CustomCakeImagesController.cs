using System.Security.Claims;
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Controllers;

[ApiController, Route("api/CustomCakeImages"), Authorize(Roles = "Customer,Admin,Staff")]
public class CustomCakeImagesController(ApplicationDbContext db, PrivateImageStore images) : ControllerBase
{
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> GetFile(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var team = User.IsInRole("Admin") || User.IsInRole("Staff");
        var image = await db.CustomCakeImages.AsNoTracking().FirstOrDefaultAsync(i => i.CustomCakeImageId == id &&
            (team || i.CustomCakeRequest.CustomerId == userId));
        if (image?.StoredFileName == null) return NotFound();
        var stream = await images.Open("custom-cakes", image.StoredFileName);
        if (stream == null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(stream, PrivateImageStore.Mime(image.StoredFileName));
    }
}

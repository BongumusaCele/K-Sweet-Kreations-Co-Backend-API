using System.Text;

namespace K_Sweet_Kreations_Co_Backend_API.Services;

public class PrivateImageStore(MediaStorage storage)
{
    public async Task<string> Save(IFormFile file, string kind)
    {
        var proof = kind == "payment-proofs";
        var limit = (proof ? 5 : 2) * 1024 * 1024;
        if (file.Length <= 0 || file.Length > limit) throw new WebsiteException(400, proof ? "Choose a proof file up to 5 MB." : "Choose an image up to 2 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var count = await stream.ReadAtLeastAsync(header, 12, throwOnEndOfStream: false);
            var valid = ext switch
            {
                ".jpg" or ".jpeg" => count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
                ".png" => count >= 8 && header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".webp" => count == 12 && Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
                ".pdf" => proof && count >= 5 && Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",
                _ => false
            };
            if (!valid) throw new WebsiteException(400, proof ? "Choose a valid PDF, JPG, PNG or WebP proof." : "Choose a valid JPG, PNG or WebP image.");
        }
        var name = Guid.NewGuid().ToString("N") + ext;
        await using var input = file.OpenReadStream();
        await storage.Save(kind, name, input);
        return name;
    }
    public Task Delete(string kind, string name) => storage.Delete(kind, name);
    public Task<Stream?> Open(string kind, string name) => storage.Open(kind, name);
    public static string Mime(string name) => Path.GetExtension(name) switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "image/jpeg"
    };
}

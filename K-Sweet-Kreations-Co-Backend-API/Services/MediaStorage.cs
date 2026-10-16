using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace K_Sweet_Kreations_Co_Backend_API.Services;

// Containers stay private. Only the application's authorized endpoints serve files.
public class MediaStorage(IWebHostEnvironment environment, IConfiguration configuration)
{
    private readonly BlobServiceClient? Client = string.IsNullOrWhiteSpace(configuration["Storage:BlobConnectionString"])
        ? null : new BlobServiceClient(configuration["Storage:BlobConnectionString"]);
    public string Folder(string kind) => kind == "products"
        ? configuration["Storage:PublicRoot"] ?? Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "products")
        : Path.Combine(configuration["Storage:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "PrivateUploads"), kind);
    private static string Container(string kind) => kind == "products" ? "product-images" : "private-documents";
    private static string Key(string kind, string name) => $"{kind}/{Path.GetFileName(name)}";

    public async Task Save(string kind, string name, Stream input)
    {
        if (Client is { } client)
        {
            var blob = client.GetBlobContainerClient(Container(kind)).GetBlobClient(Key(kind, name));
            await blob.UploadAsync(input, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = PrivateImageStore.Mime(name) } });
            return;
        }
        Directory.CreateDirectory(Folder(kind));
        await using var output = new FileStream(Path.Combine(Folder(kind), Path.GetFileName(name)), FileMode.CreateNew);
        await input.CopyToAsync(output);
    }
    public async Task<Stream?> Open(string kind, string name)
    {
        if (Client is { } client)
        {
            try { return (await client.GetBlobContainerClient(Container(kind)).GetBlobClient(Key(kind, name)).DownloadStreamingAsync()).Value.Content; }
            catch (RequestFailedException e) when (e.Status == 404) { }
        }
        var path = Path.Combine(Folder(kind), Path.GetFileName(name));
        return File.Exists(path) ? File.OpenRead(path) : null;
    }
    public async Task Delete(string kind, string name)
    {
        if (Client is { } client) await client.GetBlobContainerClient(Container(kind)).GetBlobClient(Key(kind, name)).DeleteIfExistsAsync();
        var path = Path.Combine(Folder(kind), Path.GetFileName(name));
        if (File.Exists(path)) File.Delete(path);
    }
    // Copy existing App Service files once, preserving DB references. Never delete originals.
    public async Task MigrateLocalFiles()
    {
        if (Client is not { } client) return;
        foreach (var container in new[] { "product-images", "private-documents" })
            await client.GetBlobContainerClient(container).CreateIfNotExistsAsync(PublicAccessType.None);
        foreach (var kind in new[] { "products", "custom-cakes", "payment-proofs" })
        {
            if (!Directory.Exists(Folder(kind))) continue;
            foreach (var path in Directory.EnumerateFiles(Folder(kind)))
            {
                var blob = client.GetBlobContainerClient(Container(kind)).GetBlobClient(Key(kind, Path.GetFileName(path)));
                if (await blob.ExistsAsync()) continue;
                await using var stream = File.OpenRead(path);
                try { await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = PrivateImageStore.Mime(path) } }); }
                catch (RequestFailedException e) when (e.Status == 409) { /* Another instance copied the same file. */ }
            }
        }
    }
}

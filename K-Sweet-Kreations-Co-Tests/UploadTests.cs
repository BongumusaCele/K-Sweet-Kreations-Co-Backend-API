using System.Text;
using K_Sweet_Kreations_Co_Backend_API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace K_Sweet_Kreations_Co_Tests;

public class UploadTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ksk-upload-tests-" + Guid.NewGuid().ToString("N"));
    private PrivateImageStore Store()
    {
        Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Storage:PrivateRoot"] = Path.Combine(root, "private"), ["Storage:PublicRoot"] = Path.Combine(root, "products"), ["Storage:BlobConnectionString"] = ""
        });
        return new PrivateImageStore(new MediaStorage(builder.Environment, builder.Configuration));
    }
    private static IFormFile File(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    [Fact]
    public async Task PdfProofIsStoredAndDownloadedWithoutChangingItsBytes()
    {
        var store = Store();
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type /Catalog>>endobj\n%%EOF");
        var name = await store.Save(File("bank-statement.pdf", bytes), "payment-proofs");
        Assert.EndsWith(".pdf", name);
        Assert.Equal("application/pdf", PrivateImageStore.Mime(name));
        await using (var stream = await store.Open("payment-proofs", name)) {
            Assert.NotNull(stream);
            using var output = new MemoryStream();
            await stream.CopyToAsync(output);
            Assert.Equal(bytes, output.ToArray());
        }
        await store.Delete("payment-proofs", name);
        Assert.Null(await store.Open("payment-proofs", name));
    }
    [Theory]
    [InlineData("products", "photo.pdf", "%PDF-1.4")]
    [InlineData("custom-cakes", "photo.pdf", "%PDF-1.4")]
    [InlineData("payment-proofs", "fake.pdf", "not a PDF")]
    [InlineData("products", "fake.png", "not an image")]
    [InlineData("payment-proofs", "proof.exe", "%PDF-1.4")]
    public async Task RejectsUnwantedTypesAndRenamedFiles(string kind, string name, string data)
    {
        var store = Store();
        await Assert.ThrowsAsync<WebsiteException>(() => store.Save(File(name, Encoding.ASCII.GetBytes(data)), kind));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }
    [Theory]
    [InlineData("products", 2)]
    [InlineData("payment-proofs", 5)]
    public async Task RejectsFilesOverTheLimitBeforeWriting(string kind, int megabytes)
    {
        var store = Store();
        await Assert.ThrowsAsync<WebsiteException>(() => store.Save(File("proof.pdf", new byte[megabytes * 1024 * 1024 + 1]), kind));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

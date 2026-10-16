using System.Text.Json;
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using K_Sweet_Kreations_Co_Backend_API.Services;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Tests;

public class WebsiteDirectoryTests
{
    private static async Task<ApplicationDbContext> CreateContext()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.UserAccounts.AddRange(
            new UserAccount { UserId = 1, FullName = "Admin", Email = "admin@test.example", Role = "Admin" },
            new UserAccount { UserId = 2, FullName = "Staff with no activity", Email = "staff@test.example", Role = "Staff", PasswordHash = "SECRET_HASH" },
            new UserAccount { UserId = 3, FullName = "New customer", Email = "new@test.example", PhoneNumber = "0821234567", PasswordHash = "SECRET_HASH" },
            new UserAccount { UserId = 4, FullName = "Inactive customer", Email = "inactive@test.example", Active = false });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task AdminSeesRegisteredAccountsWithoutOrdersOrRequests()
    {
        await using var db = await CreateContext();
        var result = JsonSerializer.SerializeToElement(await new WebsiteService(db).Snapshot(1, "Admin"));
        var customers = result.GetProperty("customers").EnumerateArray().ToArray();
        Assert.Equal(2, customers.Length);
        var customer = customers.Single(c => c.GetProperty("id").GetInt32() == 3);
        Assert.Equal("New customer", customer.GetProperty("name").GetString());
        Assert.Equal("0821234567", customer.GetProperty("phone").GetString());
        Assert.Equal(0, customer.GetProperty("orderCount").GetInt32());
        Assert.Equal(0, customer.GetProperty("requestCount").GetInt32());
        Assert.EndsWith("Z", customer.GetProperty("createdAt").GetString());
        Assert.False(customers.Single(c => c.GetProperty("id").GetInt32() == 4).GetProperty("active").GetBoolean());
        Assert.Equal(1, result.GetProperty("staff").GetArrayLength());
        Assert.DoesNotContain("SECRET_HASH", result.ToString());
        Assert.DoesNotContain("passwordHash", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2, "Staff")]
    [InlineData(3, "Customer")]
    [InlineData(null, null)]
    [InlineData(null, "Admin")]
    public async Task NonAdminsNeverReceiveAccountDirectories(int? id, string? role)
    {
        await using var db = await CreateContext();
        var result = JsonSerializer.SerializeToElement(await new WebsiteService(db).Snapshot(id, role));
        Assert.Equal(0, result.GetProperty("customers").GetArrayLength());
        Assert.Equal(0, result.GetProperty("staff").GetArrayLength());
        Assert.DoesNotContain("SECRET_HASH", result.ToString());
    }

    [Fact]
    public async Task DirectoryReflectsProfileChangesAndCountsWithoutLosingUnrelatedAccounts()
    {
        await using var db = await CreateContext();
        var account = await db.UserAccounts.FindAsync(3);
        account!.FullName = "Updated customer";
        account.PhoneNumber = "0837654321";
        db.CustomerOrders.Add(new CustomerOrder { CustomerId = 3 });
        db.CustomCakeRequests.Add(new CustomCakeRequest { CustomerId = 3 });
        await db.SaveChangesAsync();
        var result = JsonSerializer.SerializeToElement(await new WebsiteService(db).Snapshot(1, "Admin"));
        var customers = result.GetProperty("customers").EnumerateArray().ToArray();
        var customer = customers.Single(c => c.GetProperty("id").GetInt32() == 3);
        Assert.Equal("Updated customer", customer.GetProperty("name").GetString());
        Assert.Equal("0837654321", customer.GetProperty("phone").GetString());
        Assert.Equal(1, customer.GetProperty("orderCount").GetInt32());
        Assert.Equal(1, customer.GetProperty("requestCount").GetInt32());
        Assert.Equal(2, customers.Length);
    }
}

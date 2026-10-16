using K_Sweet_Kreations_Co_Backend_API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace K_Sweet_Kreations_Co_Tests;

public class MergedMigrationTests
{
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=localhost;Database=MigrationMetadataOnly;Integrated Security=true;TrustServerCertificate=true")
        .Options);

    [Fact]
    public void MergedSqlServerModelMatchesMigrationSnapshot()
    {
        using var db = Context();
        // This compares model metadata only and never connects to SQL Server.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void DeploymentScriptIncludesWebsiteAndNotificationMigrations()
    {
        using var db = Context();
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [WebsiteSubmissions]", script);
        Assert.Contains("ADD [DeletedAt]", script);
        Assert.Contains("CREATE TABLE [CustomerNotifications]", script);
        Assert.DoesNotContain("DROP TABLE [CustomerOrders]", script);
    }
}

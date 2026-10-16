using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Seeders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Services;

public static class DatabaseDeployment
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await db.Database.MigrateAsync();
        if (configuration.GetValue<bool>("SeedAdmin:Enabled"))
            await AdminSeeder.SeedAdminAsync(services);

        // Runtime connects as this contained user, never as the SQL server administrator.
        var password = configuration["Deployment:ApplicationPassword"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            const string sql = """
                DECLARE @command nvarchar(max);
                IF DATABASE_PRINCIPAL_ID(N'ksk_app') IS NULL
                    SET @command = N'CREATE USER [ksk_app] WITH PASSWORD = ' + QUOTENAME(@password, '''');
                ELSE
                    SET @command = N'ALTER USER [ksk_app] WITH PASSWORD = ' + QUOTENAME(@password, '''');
                EXEC sp_executesql @command;
                IF IS_ROLEMEMBER(N'db_datareader', N'ksk_app') <> 1
                    ALTER ROLE [db_datareader] ADD MEMBER [ksk_app];
                IF IS_ROLEMEMBER(N'db_datawriter', N'ksk_app') <> 1
                    ALTER ROLE [db_datawriter] ADD MEMBER [ksk_app];
                """;
            await db.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@password", password));
        }
        Console.WriteLine("Database migrations and deployment initialization completed.");
    }
}

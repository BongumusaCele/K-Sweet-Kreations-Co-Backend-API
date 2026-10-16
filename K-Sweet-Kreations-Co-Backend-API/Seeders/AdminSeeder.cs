
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace K_Sweet_Kreations_Co_Backend_API.Seeders
{
    public static class AdminSeeder
    {
        public static async Task SeedAdminAsync(
            IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var passwordHasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<UserAccount>>();

            var configuration = scope.ServiceProvider
                .GetRequiredService<IConfiguration>();

            var email = configuration["SeedAdmin:Email"]?
                .Trim().ToLowerInvariant();

            var password = configuration["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || password.Length < 12 ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            {
                throw new InvalidOperationException(
                    "Configure a valid Admin email and a password of at least 12 characters before enabling Admin seeding.");
            }

            var adminExists = await context.UserAccounts
                .AnyAsync(u => u.Role == "Admin");

            if (adminExists)
            {
                return;
            }

            var emailExists = await context.UserAccounts
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                throw new InvalidOperationException(
                    "The configured Admin email is already registered.");
            }

            var admin = new UserAccount
            {
                FullName = "System Administrator",
                Email = email,
                PhoneNumber = "0000000000",
                Role = "Admin",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            admin.PasswordHash =
                passwordHasher.HashPassword(admin, password);

            context.UserAccounts.Add(admin);

            await context.SaveChangesAsync();
        }
    }
}

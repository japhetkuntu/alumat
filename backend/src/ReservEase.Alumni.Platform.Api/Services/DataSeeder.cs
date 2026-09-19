using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services;

/// <summary>
/// Seeds the first platform SuperAdmin on first boot, so a fresh deployment
/// has something to log into. Every value is configurable (see
/// "DefaultPlatformAdminSeed" in appsettings.json) rather than hardcoded to
/// one specific person — each deployment/environment should set its own.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var staffRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<PlatformStaff>>();

        if (await staffRepo.CountAsync() == 0)
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var seed = config.GetSection("DefaultPlatformAdminSeed");
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var email = seed["Email"];
            var password = seed["Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "DefaultPlatformAdminSeed:Email/Password not configured — skipping platform SuperAdmin seed. Set them in appsettings/environment variables to seed one on next boot.");
                return;
            }

            var staff = new PlatformStaff
            {
                Name = seed["Name"] ?? "Platform Admin",
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "SuperAdmin",
                CreatedBy = "seeder",
            };
            await staffRepo.AddAsync(staff);

            logger.LogInformation("Seeded default platform SuperAdmin: {Email}", staff.Email);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();

        if (!await db.PlatformStaff.AnyAsync())
        {
            var staff = new PlatformStaff
            {
                Name = "Platform Admin",
                Email = "admin@yourplatform.example",
                Password = BCrypt.Net.BCrypt.HashPassword("platform@2026"),
                Role = "SuperAdmin",
                CreatedBy = "seeder",
            };
            db.PlatformStaff.Add(staff);
            await db.SaveChangesAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Seeded default platform SuperAdmin: {Email}", staff.Email);
        }
    }
}

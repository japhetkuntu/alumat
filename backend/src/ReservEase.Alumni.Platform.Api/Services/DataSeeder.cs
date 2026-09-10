using System.Security.Cryptography;
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
            // A fixed password here would be a permanent, source-visible credential
            // for the platform's own SuperAdmin on every fresh deployment — generate
            // one instead and surface it once, only in the boot log.
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
            var staff = new PlatformStaff
            {
                Name = "Japhet Kuntu Blankson",
                Email = "japhetkuntublankson1@gmail.com",
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "SuperAdmin",
                CreatedBy = "seeder",
            };
            db.PlatformStaff.Add(staff);
            await db.SaveChangesAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Seeded default platform SuperAdmin {Email} with generated password: {Password} — log in and change it immediately.", staff.Email, password);
        }
    }
}

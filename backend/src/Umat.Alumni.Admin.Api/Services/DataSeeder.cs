using Microsoft.EntityFrameworkCore;
using Umat.Alumni.PostgresDb.Sdk.DbContexts;
using AdminEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Admin;

namespace Umat.Alumni.Admin.Api.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();

        // Seed default SuperAdmin if none exists
        if (!await db.Set<AdminEntity>().AnyAsync())
        {
            var admin = new AdminEntity
            {
                FirstName = "Admin",
                LastName = "UMaT",
                Email = "admin@umat.edu.gh",
                Password = BCrypt.Net.BCrypt.HashPassword("admin@2026"),
                Role = "SuperAdmin",
                CreatedBy = "seeder",
                CreatedAt = DateTime.UtcNow,
            };
            db.Set<AdminEntity>().Add(admin);
            await db.SaveChangesAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Seeded default SuperAdmin: {Email}", admin.Email);
        }
    }
}

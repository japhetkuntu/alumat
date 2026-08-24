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

        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new Plan
                {
                    Name = "Starter", Price = 199, BillingInterval = "monthly",
                    MemberLimit = 2500, StorageLimitGb = 10,
                    Modules = ["Directory", "Announcements"], SupportLevel = "Standard support",
                    SortOrder = 0, CreatedBy = "seeder",
                },
                new Plan
                {
                    Name = "Growth", Price = 1250, BillingInterval = "monthly",
                    MemberLimit = 15000, StorageLimitGb = 100,
                    Modules = ["Campaigns", "Mentorship", "Jobs", "Resources"], SupportLevel = "Priority support",
                    IsMostUsed = true, SortOrder = 1, CreatedBy = "seeder",
                },
                new Plan
                {
                    Name = "Enterprise", Price = null, BillingInterval = "annual",
                    MemberLimit = null, StorageLimitGb = null,
                    Modules = ["All modules"], SupportLevel = "Dedicated support",
                    SortOrder = 2, CreatedBy = "seeder",
                }
            );
            await db.SaveChangesAsync();
        }
    }
}

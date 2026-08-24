using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Services;

/// <summary>
/// Seeds a starter institution and its first SuperAdmin on first boot, so a
/// fresh deployment has something to log into before any real onboarding
/// has happened through the Platform Portal. Every value is configurable
/// (see "DefaultInstitutionSeed" in appsettings.json) rather than hardcoded
/// to one specific institution — this platform hosts any number of them,
/// and the seeded starter tenant is just a convenience, not "the" tenant.
/// </summary>
public static class DataSeeder
{
    /// <summary>Well-known id for the seeded starter tenant, so re-running the seeder is idempotent.</summary>
    public const string DefaultInstitutionId = "00000000000000000000000000000001";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var seed = config.GetSection("DefaultInstitutionSeed");

        // Seed the starter institution if it doesn't exist yet.
        var institution = await db.Institutions.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == DefaultInstitutionId);
        if (institution is null)
        {
            institution = new InstitutionEntity
            {
                Id = DefaultInstitutionId,
                Name = seed["Name"] ?? "Demo Institution",
                Slug = seed["Slug"] ?? "demo",
                PortalName = seed["PortalName"] ?? "Alumni Portal",
                Tagline = seed["Tagline"] ?? "Connecting alumni beyond graduation.",
                ContactName = seed["ContactName"] ?? "Alumni Relations Office",
                ContactEmail = seed["ContactEmail"] ?? "alumni.office@example.com",
                SupportEmail = seed["SupportEmail"] ?? "support@example.com",
                PrimaryColorHex = seed["PrimaryColorHex"] ?? "#0e7143",
                Plan = seed["Plan"] ?? "Enterprise",
                Status = "Active",
                MemberLimit = int.TryParse(seed["MemberLimit"], out var limit) ? limit : 100_000,
                StorageLimitGb = int.TryParse(seed["StorageLimitGb"], out var storage) ? storage : 500,
                OnboardedAt = DateTime.UtcNow,
                CreatedBy = "seeder",
            };
            db.Institutions.Add(institution);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded starter institution: {Slug}", institution.Slug);
        }

        // Everything below runs scoped to that tenant, matching how a real
        // request would be scoped by TenantResolutionMiddleware.
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
        currentTenant.SetInstitutionId(institution.Id);

        // Seed default SuperAdmin if none exists
        if (!await db.Set<StaffEntity>().AnyAsync())
        {
            var adminEmail = seed["AdminEmail"] ?? "admin@example.com";
            var admin = new StaffEntity
            {
                InstitutionId = institution.Id,
                FirstName = seed["AdminFirstName"] ?? "Admin",
                LastName = seed["AdminLastName"] ?? "User",
                Email = adminEmail,
                Password = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrWhiteSpace(seed["AdminPassword"]) ? "admin@2026" : seed["AdminPassword"]),
                Role = "SuperAdmin",
                CreatedBy = "seeder",
                CreatedAt = DateTime.UtcNow,
            };
            db.Set<StaffEntity>().Add(admin);
            await db.SaveChangesAsync();

            logger.LogInformation("Seeded default SuperAdmin: {Email}", admin.Email);
        }
    }
}

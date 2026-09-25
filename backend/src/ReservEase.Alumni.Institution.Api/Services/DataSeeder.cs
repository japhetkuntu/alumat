using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
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
        var institutionRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<InstitutionEntity>>();
        var staffRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<StaffEntity>>();
        var forumCategoryRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<ForumCategory>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var seed = config.GetSection("DefaultInstitutionSeed");

        // Seed the starter institution if it doesn't exist yet.
        var institution = await institutionRepo.GetOneAsync(i => i.Id == DefaultInstitutionId, ignoreQueryFilters: true);
        if (institution is null)
        {
            var organizationType = seed["OrganizationType"] == OrganizationTypes.Community
                ? OrganizationTypes.Community
                : OrganizationTypes.Alumni;
            var isCommunity = organizationType == OrganizationTypes.Community;

            institution = new InstitutionEntity
            {
                Id = DefaultInstitutionId,
                Name = seed["Name"] ?? "Demo Institution",
                Slug = seed["Slug"] ?? "demo",
                OrganizationType = organizationType,
                PortalName = seed["PortalName"] ?? (isCommunity ? "Member Portal" : "Alumni Portal"),
                Tagline = seed["Tagline"] ?? (isCommunity ? "Connecting members beyond the group chat." : "Connecting alumni beyond graduation."),
                ContactName = seed["ContactName"] ?? (isCommunity ? "Member Relations Office" : "Alumni Relations Office"),
                ContactEmail = seed["ContactEmail"] ?? "alumni.office@example.com",
                SupportEmail = seed["SupportEmail"] ?? "support@example.com",
                PrimaryColorHex = seed["PrimaryColorHex"] ?? "#0e7143",
                Status = "Active",
                OnboardedAt = DateTime.UtcNow,
                CreatedBy = "seeder",
            };
            await institutionRepo.AddAsync(institution);
            logger.LogInformation("Seeded starter institution: {Slug}", institution.Slug);
        }

        // Everything below runs scoped to that tenant, matching how a real
        // request would be scoped by TenantResolutionMiddleware.
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
        currentTenant.SetInstitutionId(institution.Id);

        // Starter forum categories, only when the tenant has none — an admin who deleted them on purpose isn't second-guessed on the next boot.
        if (await forumCategoryRepo.CountAsync() == 0)
            await forumCategoryRepo.AddRangeAsync(ForumCategoryDefaults.Build(institution.Id, "seeder"));

        // Seed default SuperAdmin if none exists
        if (await staffRepo.CountAsync() == 0)
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
            await staffRepo.AddAsync(admin);

            logger.LogInformation("Seeded default SuperAdmin: {Email}", admin.Email);
        }
    }
}

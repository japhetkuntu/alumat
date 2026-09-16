using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var staffRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<PlatformStaff>>();

        if (await staffRepo.CountAsync() == 0)
        {
            var staff = new PlatformStaff
            {
                Name = "Japhet Kuntu Blankson",
                Email = "japhetkuntublankson1@gmail.com",
                Password = BCrypt.Net.BCrypt.HashPassword("platform@2026"),
                Role = "SuperAdmin",
                CreatedBy = "seeder",
            };
            await staffRepo.AddAsync(staff);

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Seeded default platform SuperAdmin: {Email}", staff.Email);
        }
    }
}

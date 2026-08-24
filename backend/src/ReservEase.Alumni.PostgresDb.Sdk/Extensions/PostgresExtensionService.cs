using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Sdk.Extensions;

public static class PostgresExtensionService
{
    public static IServiceCollection AddAlumniPostgresSdk(
        this IServiceCollection services, IConfiguration config, string connectionName = "AlumniConnection")
    {
        services.AddDbContext<AlumniDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString(connectionName),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "alumni")));

        services.AddScoped(typeof(IAlumniPgRepository<>), typeof(AlumniPgRepository<>));
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();

        return services;
    }

    public static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();

        int attempts = 0;
        while (attempts < 5)
        {
            try
            {
                await context.Database.MigrateAsync();
                break;
            }
            catch
            {
                attempts++;
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}

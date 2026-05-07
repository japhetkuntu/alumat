using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umat.Alumni.PostgresDb.Sdk.DbContexts;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.PostgresDb.Sdk.Extensions;

public static class PostgresExtensionService
{
    public static IServiceCollection AddAlumniPostgresSdk(
        this IServiceCollection services, IConfiguration config, string connectionName = "AlumniConnection")
    {
        services.AddDbContext<AlumniDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString(connectionName),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "alumni")));

        services.AddScoped(typeof(IAlumniPgRepository<>), typeof(AlumniPgRepository<>));

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

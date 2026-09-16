using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Fixed, arbitrary key for the Postgres session-level advisory lock that serializes
    /// migrations below. Any int64 works as long as every service uses the same one —
    /// it's a lock name, not a value with meaning of its own.
    /// </summary>
    private const long MigrationLockId = 851200915;

    /// <summary>
    /// Member.Api, Institution.Api and Platform.Api all call this against the same shared
    /// database at their own startup, with no coordination between them — so bringing them
    /// up together against a fresh database races multiple processes through the same
    /// pending EF migrations concurrently. One process's CREATE TABLE for a later migration
    /// commits while another is mid-transaction on an earlier one that the first process's
    /// migration already assumed exists, producing spurious "relation does not exist"
    /// errors that have nothing to do with the migrations themselves. A Postgres advisory
    /// lock serializes them: whoever gets there first runs migrations while the others
    /// block on pg_advisory_lock, then each of those finds nothing pending and returns
    /// immediately once it's their turn.
    ///
    /// Retries beyond that only cover a database that isn't accepting connections yet
    /// (common right after a fresh instance/container starts) — a genuine migration
    /// failure (bad SQL, conflicting schema) fails the same way on every attempt and must
    /// not be swallowed: seeding against a partially-migrated schema produces confusing
    /// "column/relation does not exist" errors with no trace back to the real cause. So
    /// every attempt is logged, and exhausting all retries throws instead of letting
    /// startup continue into DataSeeder.
    /// </summary>
    public static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PostgresExtensionService));

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Postgres has no "default admin database" to connect to and issue CREATE
                // DATABASE from the way SQL Server has `master` — a session can only be
                // opened against a database that already exists, so the OpenConnectionAsync
                // below would fail outright against a not-yet-provisioned database.
                // IRelationalDatabaseCreator manages its own connection (against Postgres's
                // own default maintenance database) to check for and create ours first — it
                // only creates the empty database, never tables, so MigrateAsync below still
                // owns schema creation and migration-history tracking is unaffected.
                var databaseCreator = context.GetService<IRelationalDatabaseCreator>();
                if (!await databaseCreator.ExistsAsync())
                {
                    logger.LogInformation("Target database does not exist yet — creating it before migrating.");
                    await databaseCreator.CreateAsync();
                }

                // The lock is session-level (tied to this connection) and released
                // automatically when the connection closes, even if the process crashes
                // mid-migration — no separate unlock bookkeeping needed on the failure path.
                await context.Database.OpenConnectionAsync();
                try
                {
                    await context.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockId})");
                    await context.Database.MigrateAsync();
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Database migration attempt {Attempt}/{MaxAttempts} failed, retrying in 3s", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Database migration failed after {MaxAttempts} attempts — aborting startup rather than seeding an unmigrated schema", maxAttempts);
                throw;
            }
        }
    }
}

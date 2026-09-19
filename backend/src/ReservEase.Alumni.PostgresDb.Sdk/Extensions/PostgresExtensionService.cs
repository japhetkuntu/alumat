using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Sdk.Extensions;

public static class PostgresExtensionService
{
    // Use a stable, application-specific advisory lock ID.
  
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


  

    public static async Task ApplyMigrationsAsync(
        IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context =
            scope.ServiceProvider.GetRequiredService<AlumniDbContext>();

        var logger =
            scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(PostgresExtensionService));

        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Make sure AlumniDb exists.
                await EnsureDatabaseExistsAsync(
                    context,
                    logger);

                // Connect to AlumniDb.
                await context.Database.OpenConnectionAsync();

                try
                {
                    // Prevent multiple application instances from
                    // running migrations at the same time.
                    await context.Database.ExecuteSqlRawAsync(
                        $"SELECT pg_advisory_lock({MigrationLockId});");

                    logger.LogInformation(
                        "Running EF Core database migrations...");

                    await context.Database.MigrateAsync();

                    logger.LogInformation(
                        "EF Core database migrations completed successfully.");
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }

                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed. " +
                    "Retrying in 3 seconds...",
                    attempt,
                    maxAttempts);

                await Task.Delay(
                    TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                logger.LogCritical(
                    ex,
                    "Database migration failed after {MaxAttempts} attempts. " +
                    "Aborting application startup.",
                    maxAttempts);

                throw;
            }
        }
    }

    private static async Task EnsureDatabaseExistsAsync(
        AlumniDbContext context,
        ILogger logger)
    {
        var connectionString =
            context.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string is not configured.");
        }

        var connectionBuilder =
            new NpgsqlConnectionStringBuilder(connectionString);

        // This is the database your application wants to use.
        var targetDatabaseName =
            connectionBuilder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabaseName))
        {
            throw new InvalidOperationException(
                "Database name is missing from the PostgreSQL connection string.");
        }

        logger.LogInformation(
            "Checking whether database {DatabaseName} exists...",
            targetDatabaseName);

        /*
         *  Managed PostgreSQL does not necessarily expose
         * the conventional PostgreSQL "postgres" maintenance database.
         *
         * We therefore connect to the existing database provided by
         * Managed PostgreSQL instead.
         *
         * Change this if your Managed PostgreSQL cluster uses a different
         * existing database.
         */
        connectionBuilder.Database = "defaultdb";

        // DigitalOcean Managed PostgreSQL requires SSL.
        connectionBuilder.SslMode = SslMode.Require;

        await using var connection =
            new NpgsqlConnection(
                connectionBuilder.ConnectionString);

        await connection.OpenAsync();

        logger.LogInformation(
            "Connected to PostgreSQL maintenance database.");

        // ---------------------------------------------------------
        // Check whether target database already exists
        // ---------------------------------------------------------

        await using (var checkCommand =
                     connection.CreateCommand())
        {
            checkCommand.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_database
                    WHERE datname = @databaseName
                );
                """;

            checkCommand.Parameters.AddWithValue(
                "databaseName",
                targetDatabaseName);

            var result =
                await checkCommand.ExecuteScalarAsync();

            var exists =
                result is bool value && value;

            if (exists)
            {
                logger.LogInformation(
                    "Database {DatabaseName} already exists.",
                    targetDatabaseName);

                return;
            }
        }

        // ---------------------------------------------------------
        // Create target database
        // ---------------------------------------------------------

        logger.LogInformation(
            "Database {DatabaseName} does not exist. Creating it...",
            targetDatabaseName);

        /*
         * PostgreSQL parameters cannot be used for database
         * identifiers, so the identifier needs to be quoted.
         *
         * Escape any existing double quotes to prevent malformed SQL.
         */
        var quotedDatabaseName =
            "\"" +
            targetDatabaseName.Replace("\"", "\"\"") +
            "\"";

        await using (var createCommand =
                     connection.CreateCommand())
        {
            createCommand.CommandText =
                $"CREATE DATABASE {quotedDatabaseName};";

            await createCommand.ExecuteNonQueryAsync();
        }

        logger.LogInformation(
            "Database {DatabaseName} created successfully.",
            targetDatabaseName);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umat.Alumni.PostgresDb.Sdk.DbContexts;

namespace Umat.Alumni.PostgresDb.Sdk.Factories;

public class AlumniDbContextFactory : IDesignTimeDbContextFactory<AlumniDbContext>
{
    public AlumniDbContext CreateDbContext(string[] args)
    {
        // Design-time connection string (used only for `dotnet ef migrations`).
        // Set the environment variable locally or pass via args:
        //   export ALUMNI_CONNECTION="Host=localhost;Port=5432;Database=AlumniDb;Username=admin;Password=...;SearchPath=alumni"
        var connectionString =
            Environment.GetEnvironmentVariable("ALUMNI_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set the ALUMNI_CONNECTION environment variable before running EF migrations.");

        var optionsBuilder = new DbContextOptionsBuilder<AlumniDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "alumni"));

        return new AlumniDbContext(optionsBuilder.Options);
    }
}

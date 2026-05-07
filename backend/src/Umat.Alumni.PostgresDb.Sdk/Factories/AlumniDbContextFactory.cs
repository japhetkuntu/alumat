using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umat.Alumni.PostgresDb.Sdk.DbContexts;

namespace Umat.Alumni.PostgresDb.Sdk.Factories;

public class AlumniDbContextFactory : IDesignTimeDbContextFactory<AlumniDbContext>
{
    public AlumniDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AlumniDbContext>();
        // Design-time connection string (used only for migrations)
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=AlumniDb;Username=admin;Password=admin;SearchPath=alumni",
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "alumni"));

        return new AlumniDbContext(optionsBuilder.Options);
    }
}

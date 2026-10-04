using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.TestKit;

/// <summary>Builds an in-memory <see cref="AlumniDbContext"/> scoped to a tenant, optionally sharing one database between several scoped contexts.</summary>
public static class TestDb
{
    public static string NewName() => Guid.NewGuid().ToString("N");

    public static CurrentTenantService Tenant(string? institutionId, string? slug = null)
    {
        var tenant = new CurrentTenantService();
        if (institutionId is not null) tenant.SetInstitutionId(institutionId, slug);
        return tenant;
    }

    public static AlumniDbContext Create(string? databaseName = null, string? institutionId = null, ICurrentTenantService? tenant = null)
    {
        var options = new DbContextOptionsBuilder<AlumniDbContext>()
            .UseInMemoryDatabase(databaseName ?? NewName())
            .Options;
        return new AlumniDbContext(options, tenant ?? Tenant(institutionId));
    }

    /// <summary>
    /// A relational (SQLite) context for code that uses ExecuteUpdate/ExecuteDelete, which the in-memory provider cannot run.
    /// The returned connection must stay open for the life of the database — dispose it when the test ends.
    /// </summary>
    public static (AlumniDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection) CreateRelational(string? institutionId = null)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = OpenRelational(connection, institutionId);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    public static AlumniDbContext OpenRelational(Microsoft.Data.Sqlite.SqliteConnection connection, string? institutionId = null) =>
        // No tracking: Temporal activities share one context here, whereas production gives each activity call its own scope.
        new(new DbContextOptionsBuilder<AlumniDbContext>().UseSqlite(connection).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options, Tenant(institutionId));

    public static AlumniPgRepository<T> Repo<T>(AlumniDbContext db) where T : PostgresDb.Sdk.Entities.BaseEntity => new(db);
}

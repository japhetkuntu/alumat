using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class RelationalTestDatabaseTests
{
    [Fact]
    public async Task ExecuteUpdate_works_against_the_relational_test_database()
    {
        var (db, connection) = TestDb.CreateRelational();
        using var _ = connection;
        db.StoreProducts.Add(new StoreProduct { Id = "p1", Name = "x", Price = 1, QuantityAvailable = 5, InstitutionId = "i" });
        await db.SaveChangesAsync();

        var repo = new AlumniPgRepository<StoreProduct>(TestDb.OpenRelational(connection));
        var n = await repo.ExecuteUpdateAsync(p => p.Id == "p1", s => s.SetProperty(p => p.QuantityAvailable, 2), ignoreQueryFilters: true);

        Assert.Equal(1, n);
        Assert.Equal(2, (await TestDb.OpenRelational(connection).StoreProducts.IgnoreQueryFilters().SingleAsync()).QuantityAvailable);
    }
}

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>The whole multi-tenant model rests on the query filter and the write stamp, so they are tested directly.</summary>
public class TenantIsolationTests
{
    private static async Task SeedAsync(string db)
    {
        // Seed bypassing the tenant by setting InstitutionId explicitly.
        using var ctx = TestDb.Create(db);
        ctx.Jobs.AddRange(
            new Job { Id = "a1", InstitutionId = "A", Title = "A job 1" },
            new Job { Id = "a2", InstitutionId = "A", Title = "A job 2" },
            new Job { Id = "b1", InstitutionId = "B", Title = "B job 1" });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Queries_only_see_the_current_tenants_rows()
    {
        var db = TestDb.NewName();
        await SeedAsync(db);

        using var a = TestDb.Create(db, "A");
        using var b = TestDb.Create(db, "B");

        Assert.Equal(new[] { "a1", "a2" }, (await a.Jobs.OrderBy(j => j.Id).ToListAsync()).Select(j => j.Id));
        Assert.Equal(new[] { "b1" }, (await b.Jobs.Select(j => j.Id).ToListAsync()));
    }

    [Fact]
    public async Task With_no_tenant_resolved_nothing_tenant_scoped_is_visible()
    {
        var db = TestDb.NewName();
        await SeedAsync(db);

        using var none = TestDb.Create(db);

        Assert.Empty(await none.Jobs.ToListAsync());
    }

    [Fact]
    public async Task IgnoreQueryFilters_sees_every_tenant()
    {
        var db = TestDb.NewName();
        await SeedAsync(db);

        using var a = TestDb.Create(db, "A");

        Assert.Equal(3, await a.Jobs.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task New_rows_are_stamped_with_the_current_tenant()
    {
        var db = TestDb.NewName();
        using (var a = TestDb.Create(db, "A"))
        {
            a.Jobs.Add(new Job { Id = "j", Title = "x" });
            await a.SaveChangesAsync();
        }

        using var all = TestDb.Create(db, "A");
        Assert.Equal("A", (await all.Jobs.IgnoreQueryFilters().SingleAsync()).InstitutionId);
    }

    [Fact]
    public async Task An_explicit_institution_id_is_never_overwritten()
    {
        var db = TestDb.NewName();
        using (var a = TestDb.Create(db, "A"))
        {
            a.Jobs.Add(new Job { Id = "j", InstitutionId = "OTHER", Title = "x" });
            await a.SaveChangesAsync();
        }

        using var read = TestDb.Create(db, "OTHER");
        Assert.Single(await read.Jobs.ToListAsync());
    }

    [Fact]
    public async Task Without_a_tenant_nothing_is_stamped()
    {
        var db = TestDb.NewName();
        using (var none = TestDb.Create(db))
        {
            none.Jobs.Add(new Job { Id = "j", Title = "x" });
            await none.SaveChangesAsync();
        }

        using var read = TestDb.Create(db);
        Assert.Equal(string.Empty, (await read.Jobs.IgnoreQueryFilters().SingleAsync()).InstitutionId);
    }

    [Fact]
    public async Task Modifying_an_existing_row_does_not_restamp_it()
    {
        var db = TestDb.NewName();
        await SeedAsync(db);

        using (var ctx = TestDb.Create(db, "B"))
        {
            var job = await ctx.Jobs.IgnoreQueryFilters().SingleAsync(j => j.Id == "a1");
            job.Title = "edited by B's context";
            await ctx.SaveChangesAsync();
        }

        using var verify = TestDb.Create(db, "A");
        var edited = await verify.Jobs.SingleAsync(j => j.Id == "a1");
        Assert.Equal("A", edited.InstitutionId);
    }

    [Fact]
    public async Task The_synchronous_save_stamps_too()
    {
        var db = TestDb.NewName();
        using (var a = TestDb.Create(db, "A"))
        {
            a.Jobs.Add(new Job { Id = "s", Title = "sync" });
            a.SaveChanges();
        }

        using var read = TestDb.Create(db, "A");
        Assert.Single(await read.Jobs.ToListAsync());
    }

    [Fact]
    public async Task Tenant_changes_after_construction_are_honoured_because_the_filter_reads_the_service_each_time()
    {
        var db = TestDb.NewName();
        await SeedAsync(db);
        var tenant = new CurrentTenantService();
        using var ctx = TestDb.Create(db, tenant: tenant);

        Assert.Empty(await ctx.Jobs.ToListAsync());
        tenant.SetInstitutionId("B");
        Assert.Single(await ctx.Jobs.ToListAsync());
    }

    [Fact]
    public async Task Platform_level_entities_are_not_tenant_filtered()
    {
        var db = TestDb.NewName();
        using (var ctx = TestDb.Create(db, "A"))
        {
            ctx.Institutions.Add(new Institution { Id = "i1", Slug = "one", Name = "One" });
            ctx.Institutions.Add(new Institution { Id = "i2", Slug = "two", Name = "Two" });
            await ctx.SaveChangesAsync();
        }

        using var other = TestDb.Create(db, "B");
        Assert.Equal(2, await other.Institutions.CountAsync());
    }

    [Fact]
    public void Every_tenant_scoped_entity_in_the_model_has_a_query_filter()
    {
        using var ctx = TestDb.Create(institutionId: "A");
        var scoped = ctx.Model.GetEntityTypes().Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)).ToList();

        Assert.True(scoped.Count > 20);
        Assert.All(scoped, t => Assert.True(t.GetQueryFilter() is not null, $"{t.ClrType.Name} is ITenantScoped but has no query filter"));
    }

    [Fact]
    public void Non_tenant_entities_have_no_query_filter()
    {
        using var ctx = TestDb.Create(institutionId: "A");
        var unscoped = ctx.Model.GetEntityTypes().Where(t => !typeof(ITenantScoped).IsAssignableFrom(t.ClrType) && t.GetQueryFilter() is not null).ToList();
        Assert.Empty(unscoped.Select(t => t.ClrType.Name));
    }

    [Fact]
    public void Every_tenant_scoped_entity_indexes_InstitutionId()
    {
        using var ctx = TestDb.Create(institutionId: "A");
        var missing = ctx.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType))
            .Where(t => !t.GetIndexes().Any(i => i.Properties.First().Name == nameof(ITenantScoped.InstitutionId)))
            .Select(t => t.ClrType.Name).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Everything_lives_in_the_alumni_schema()
    {
        using var ctx = TestDb.Create(institutionId: "A");
        Assert.Equal("alumni", ctx.Model.GetDefaultSchema());
    }

    [Fact]
    public void Base_entity_defaults_generate_unique_ids_and_system_audit_fields()
    {
        var a = new Job();
        var b = new Job();
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(32, a.Id.Length);
        Assert.Equal("system", a.CreatedBy);
        Assert.Null(a.UpdatedAt);
        Assert.True((DateTime.UtcNow - a.CreatedAt).TotalSeconds < 5);
    }
}

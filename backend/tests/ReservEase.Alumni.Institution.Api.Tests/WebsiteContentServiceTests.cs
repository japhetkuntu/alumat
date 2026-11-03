using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>What the editor shows for the live sections must be what visitors get: the institution's own public items, nothing else.</summary>
public class WebsiteContentServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Rig(params string[] disabled)
        {
            using var db = TestDb.Create(DbName, Tenant);
            db.Institutions.Add(new InstitutionEntity { Id = Tenant, Slug = "g", Name = "G", DisabledFeatures = disabled.ToList() });
            db.SaveChanges();
        }
        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);
        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
        }
        public WebsiteContentService Service()
        {
            var db = Db();
            return new WebsiteContentService(new AlumniPgRepository<InstitutionEntity>(db), new AlumniPgRepository<NewsPost>(db), new AlumniPgRepository<AlumniEvent>(db),
                new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<BusinessListing>(db),
                new AlumniPgRepository<ServiceType>(db), new AlumniPgRepository<StoreProduct>(db), new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<Job>(db),
                new AlumniPgRepository<Spotlight>(db), new AlumniPgRepository<MemberEntity>(db), TestDb.Tenant(Tenant, "g"), NullLogger<WebsiteContentService>.Instance);
        }
    }

    [Fact]
    public async Task An_institution_with_nothing_yet_gets_empty_lists_not_made_up_ones()
    {
        var c = (await new Rig().Service().GetAsync()).Data!;
        Assert.Empty(c.News); Assert.Empty(c.Events); Assert.Empty(c.Services); Assert.Empty(c.Store); Assert.Empty(c.Fundraisers); Assert.Empty(c.Jobs); Assert.Empty(c.Spotlights);
        Assert.Equal(0, c.Stats.Members);
    }

    [Fact]
    public async Task Only_upcoming_institution_wide_events_show()
    {
        var rig = new Rig();
        await rig.Seed(
            new AlumniEvent { Id = "e1", Title = "Dinner", Venue = "Hall", Status = "Upcoming", StartDate = DateTime.UtcNow.AddDays(5) },
            new AlumniEvent { Id = "e2", Title = "Past", Venue = "Hall", Status = "Upcoming", StartDate = DateTime.UtcNow.AddDays(-5) },
            new AlumniEvent { Id = "e3", Title = "Group only", Venue = "Hall", Status = "Upcoming", StartDate = DateTime.UtcNow.AddDays(6), CommunityId = "g1" },
            new AlumniEvent { Id = "e4", Title = "Year only", Venue = "Hall", Status = "Upcoming", StartDate = DateTime.UtcNow.AddDays(7), YearGroups = [2010] },
            new AlumniEvent { Id = "e5", Title = "Draft", Venue = "Hall", Status = "Draft", StartDate = DateTime.UtcNow.AddDays(8) });
        var c = (await rig.Service().GetAsync()).Data!;
        Assert.Equal(["Dinner"], c.Events.Select(e => e.Title));
        Assert.Equal(1, c.Stats.EventsNext30Days);
    }

    [Fact]
    public async Task Services_and_products_show_only_when_the_institution_ticked_show_on_website()
    {
        var rig = new Rig();
        await rig.Seed(
            new ServiceType { Id = "s1", Name = "Certificate", Price = 40, Status = "Active", ShowOnWebsite = true },
            new ServiceType { Id = "s2", Name = "Private", Price = 10, Status = "Active", ShowOnWebsite = false },
            new ServiceType { Id = "s3", Name = "Archived", Price = 10, Status = "Archived", ShowOnWebsite = true },
            new StoreProduct { Id = "p1", Name = "Cap", Price = 55, Status = "Active", ShowOnWebsite = true },
            new StoreProduct { Id = "p2", Name = "Hidden", Price = 5, Status = "Active" });
        var c = (await rig.Service().GetAsync()).Data!;
        Assert.Equal(["Certificate"], c.Services.Select(s => s.Name));
        Assert.Equal(["Cap"], c.Store.Select(p => p.Name));
    }

    [Fact]
    public async Task Jobs_and_spotlights_show_only_when_public_or_approved()
    {
        var rig = new Rig();
        await rig.Seed(
            new Job { Id = "j1", Title = "Open", Company = "A", Location = "Accra", Status = "Active", IsPublic = true },
            new Job { Id = "j2", Title = "Members only", Company = "B", Location = "Accra", Status = "Active", IsPublic = false },
            new Spotlight { Id = "sp1", Title = "Approved", Story = "x", Status = "Approved" },
            new Spotlight { Id = "sp2", Title = "Pending", Story = "x", Status = "Pending" });
        var c = (await rig.Service().GetAsync()).Data!;
        Assert.Equal(["Open"], c.Jobs.Select(j => j.Title));
        Assert.Equal(["Approved"], c.Spotlights.Select(s => s.Title));
    }

    [Fact]
    public async Task A_feature_the_platform_switched_off_gives_nothing()
    {
        var rig = new Rig("Events", "Services", "Spotlights");
        await rig.Seed(
            new AlumniEvent { Id = "e1", Title = "Dinner", Venue = "Hall", Status = "Upcoming", StartDate = DateTime.UtcNow.AddDays(5) },
            new ServiceType { Id = "s1", Name = "Certificate", Price = 40, Status = "Active", ShowOnWebsite = true },
            new Spotlight { Id = "sp1", Title = "Approved", Story = "x", Status = "Approved" });
        var c = (await rig.Service().GetAsync()).Data!;
        Assert.Empty(c.Events); Assert.Empty(c.Services); Assert.Empty(c.Spotlights);
        Assert.Null(c.Stats.EventsNext30Days);
    }
}

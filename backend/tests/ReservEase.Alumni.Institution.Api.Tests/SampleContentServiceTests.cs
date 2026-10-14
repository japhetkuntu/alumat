using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class SampleContentServiceTests
{
    private const string Tenant = "i1";

    private static (SampleContentService Service, Func<AlumniDbContextProbe> Probe, Mock<IRedisService<PublicContentCacheConfig>> Cache) Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name, Tenant);
        var cache = new Mock<IRedisService<PublicContentCacheConfig>>();
        var service = new SampleContentService(
            new AlumniPgRepository<NewsPost>(ctx), new AlumniPgRepository<AlumniEvent>(ctx), cache.Object,
            TestDb.Tenant(Tenant, "umat"), NullLogger<SampleContentService>.Instance);
        return (service, () => new AlumniDbContextProbe(TestDb.Create(name, Tenant)), cache);
    }

    /// <summary>A fresh read of what is in the store, so assertions never rely on the service's own tracked state.</summary>
    public sealed class AlumniDbContextProbe(PostgresDb.Sdk.DbContexts.AlumniDbContext db)
    {
        public List<NewsPost> News => db.Set<NewsPost>().ToList();
        public List<AlumniEvent> Events => db.Set<AlumniEvent>().ToList();
    }

    [Fact]
    public async Task Adding_creates_clearly_marked_examples_and_clears_the_public_caches()
    {
        var (service, probe, cache) = Create();

        var response = await service.AddAsync("admin-1");

        Assert.Equal(201, response.Code);
        Assert.True(response.Data!.Present);
        var read = probe();
        Assert.Equal(2, read.News.Count);
        Assert.Single(read.Events);
        Assert.All(read.News, n => { Assert.Equal(SampleContentService.Marker, n.CreatedBy); Assert.Equal("Published", n.Status); });
        Assert.StartsWith("Example event", read.Events[0].Title);
        Assert.True(read.Events[0].StartDate > DateTime.UtcNow);
        cache.Verify(c => c.RemoveAsync(PublicContentCacheKeys.News(Tenant)), Times.Once);
        cache.Verify(c => c.RemoveAsync(PublicContentCacheKeys.Events(Tenant)), Times.Once);
    }

    [Fact]
    public async Task Adding_twice_is_refused_rather_than_duplicating_the_examples()
    {
        var (service, probe, _) = Create();
        await service.AddAsync("admin-1");

        var again = await service.AddAsync("admin-1");

        Assert.Equal(400, again.Code);
        Assert.Equal(2, probe().News.Count);
    }

    [Fact]
    public async Task Removing_takes_away_only_the_examples_never_what_a_person_wrote()
    {
        var (service, probe, _) = Create(
            new NewsPost { Id = "real-news", InstitutionId = Tenant, Title = "Our AGM", Status = "Published", CreatedBy = "admin-7" },
            new AlumniEvent { Id = "real-event", InstitutionId = Tenant, Title = "Homecoming", CreatedBy = "admin-7", StartDate = DateTime.UtcNow.AddDays(5) });
        await service.AddAsync("admin-1");

        var removed = await service.RemoveAsync();

        Assert.Equal(200, removed.Code);
        Assert.False(removed.Data!.Present);
        var read = probe();
        Assert.Equal(["real-news"], read.News.Select(n => n.Id));
        Assert.Equal(["real-event"], read.Events.Select(e => e.Id));
    }
}

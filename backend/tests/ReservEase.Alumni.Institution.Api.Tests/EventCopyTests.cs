using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Creating an event as a copy of another: the photos come along, and only the original's own.</summary>
public class EventCopyTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin1", Role = "SuperAdmin" };

    private static (EventService Service, Func<AlumniDbContext> Db) Rig(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var s = TestDb.Create(name, Tenant))
        {
            s.Institutions.Add(new InstitutionEntity { Id = Tenant, Slug = "umat", Name = "UMaT" });
            foreach (var e in seed) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; s.Add(e); }
            s.SaveChanges();
        }
        var db = TestDb.Create(name, Tenant);
        var temporal = new Mock<ITemporalClientProvider>(); temporal.SetupGet(t => t.IsAvailable).Returns(false);
        var service = new EventService(
            new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<MemberEntity>(db),
            new AlumniPgRepository<InstitutionEntity>(db), new AlumniPgRepository<EventUpdate>(db), Mock.Of<IStorageService>(), temporal.Object, TestDb.Tenant(Tenant, "umat"),
            Mock.Of<IRedisService<PublicContentCacheConfig>>(), Mock.Of<IInstitutionAuditLogService>(), NullNullLogger());
        return (service, () => TestDb.Create(name, Tenant));
    }

    private static Microsoft.Extensions.Logging.ILogger<EventService> NullNullLogger() => NullLogger<EventService>.Instance;

    private static AlumniEvent Original() => new()
    {
        Id = "e1", Title = "Gala 2026", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(-30), Status = "Completed",
        BannerImageUrl = "https://cdn/banner.png", ImageUrls = ["https://cdn/a.png", "https://cdn/b.png"],
    };

    private static CreateEventRequest Copy(bool keepBanner, params string[] keep) => new()
    {
        Title = "Gala 2027", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(60),
        CopyFromEventId = "e1", KeepBanner = keepBanner, KeepImageUrls = [.. keep],
    };

    [Fact]
    public async Task A_copy_keeps_the_banner_and_the_chosen_photos_and_is_a_fresh_upcoming_event()
    {
        var (service, db) = Rig(Original());

        var result = await service.CreateEventAsync(Copy(true, "https://cdn/b.png"), Admin);

        Assert.Equal(201, result.Code);
        var copy = await db().Events.SingleAsync(e => e.Id != "e1");
        Assert.Equal(("Upcoming", "https://cdn/banner.png"), (copy.Status, copy.BannerImageUrl));
        Assert.Equal(["https://cdn/b.png"], copy.ImageUrls);
        Assert.Equal("https://cdn/banner.png", (await db().Events.SingleAsync(e => e.Id == "e1")).BannerImageUrl);   // the original is untouched
    }

    [Fact]
    public async Task A_copy_can_leave_the_banner_out_and_ignores_photos_the_original_never_had()
    {
        var (service, db) = Rig(Original());

        await service.CreateEventAsync(Copy(false, "https://elsewhere.example/x.png", "https://cdn/a.png"), Admin);

        var copy = await db().Events.SingleAsync(e => e.Id != "e1");
        Assert.Null(copy.BannerImageUrl);
        Assert.Equal(["https://cdn/a.png"], copy.ImageUrls);
    }

    [Fact]
    public async Task Copying_an_event_that_does_not_exist_is_refused()
    {
        var (service, db) = Rig();

        var result = await service.CreateEventAsync(Copy(true), Admin);

        Assert.Equal(404, result.Code);
        Assert.Empty(await db().Events.ToListAsync());
    }
}

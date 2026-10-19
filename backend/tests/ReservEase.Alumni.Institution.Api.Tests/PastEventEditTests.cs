using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Text;
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
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Editing events that are over: allowed, and quiet. Attendees are only told about changes that matter to them.</summary>
public class PastEventEditTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin1", Role = "SuperAdmin" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CapturingLogger<EventService> Log { get; } = new();
        public EventService Service { get; private set; } = null!;
        public Rig() { using var s = TestDb.Create(DbName, Tenant); s.Institutions.Add(new InstitutionEntity { Id = Tenant, Slug = "umat", Name = "UMaT" }); s.SaveChanges(); Fresh(); }

        public EventService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            var temporal = new Mock<ITemporalClientProvider>(); temporal.SetupGet(t => t.IsAvailable).Returns(false);   // a dropped notification is logged, so tests can count them
            return Service = new EventService(
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<MemberEntity>(db),
                new AlumniPgRepository<InstitutionEntity>(db), new AlumniPgRepository<EventUpdate>(db), Mock.Of<IStorageService>(), temporal.Object, TestDb.Tenant(Tenant, "umat"),
                Mock.Of<IRedisService<PublicContentCacheConfig>>(), Mock.Of<IInstitutionAuditLogService>(), Log);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }

        public int Notified => Log.Entries.Count(e => e.Message.Contains("EventDetailsChanged") || e.Message.Contains("EventCancelled"));
    }

    private static AlumniEvent Event(string status, int startsInDays) => new()
    {
        Id = "e1", Title = "Gala", Venue = "Hall", Status = status, StartDate = DateTime.UtcNow.AddDays(startsInDays),
    };

    private static UpdateEventRequest Edit(string status, int startsInDays, string venue = "Hall", string description = "Recap") => new()
    {
        EventId = "e1", Title = "Gala", Venue = venue, StartDate = DateTime.UtcNow.AddDays(startsInDays), Status = status, Description = description,
    };

    [Fact]
    public async Task A_completed_event_can_be_edited_and_going_attendees_are_not_pinged_about_it()
    {
        var rig = new Rig();
        await rig.Seed(Event("Completed", -20), new EventRsvp { EventId = "e1", MemberId = "m1", Status = "Confirmed" });

        var result = await rig.Service.UpdateEventAsync(Edit("Completed", -20, venue: "Main Hall"), Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal("Main Hall", (await rig.Db().Events.SingleAsync()).Venue);
        Assert.Equal(0, rig.Notified);
    }

    [Fact]
    public async Task Moving_a_past_event_to_a_future_date_tells_the_people_going()
    {
        var rig = new Rig();
        await rig.Seed(Event("Upcoming", -2), new EventRsvp { EventId = "e1", MemberId = "m1", Status = "Confirmed" });

        Assert.Equal(200, (await rig.Service.UpdateEventAsync(Edit("Upcoming", 14), Admin)).Code);
        Assert.Equal(1, rig.Notified);
    }

    [Fact]
    public async Task Cancelling_tells_people_once_and_editing_a_cancelled_event_later_does_not_repeat_it()
    {
        var rig = new Rig();
        await rig.Seed(Event("Upcoming", 10), new EventRsvp { EventId = "e1", MemberId = "m1", Status = "Confirmed" });

        await rig.Service.UpdateEventAsync(Edit("Cancelled", 10), Admin);
        Assert.Equal(1, rig.Notified);
        await rig.Service.UpdateEventAsync(Edit("Cancelled", 10, description: "Sorry about this."), Admin);
        Assert.Equal(1, rig.Notified);
        Assert.Equal("<p>Sorry about this.</p>", (await rig.Db().Events.SingleAsync()).Description);
    }

    [Fact]
    public async Task Changing_the_venue_of_an_event_still_ahead_tells_the_people_going()
    {
        var rig = new Rig();
        await rig.Seed(Event("Upcoming", 10), new EventRsvp { EventId = "e1", MemberId = "m1", Status = "Confirmed" });

        await rig.Service.UpdateEventAsync(Edit("Upcoming", 10, venue: "Auditorium"), Admin);
        Assert.Equal(1, rig.Notified);
    }
}

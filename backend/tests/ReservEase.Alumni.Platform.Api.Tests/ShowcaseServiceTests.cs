using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>Which institutions the website names: only with the institution's agreement, only while active, and with numbers counted from their own data.</summary>
public class ShowcaseServiceTests
{
    private static (ShowcaseService Service, Mock<IAuditLogService> Audit, string Db) Create()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            db.AddRange(
                new Institution { Id = "i1", Name = "UMaT", Slug = "umat", Status = "Active" },
                new Institution { Id = "i2", Name = "Old College", Slug = "old", Status = "Suspended" },
                new MemberEntity { Id = "m1", InstitutionId = "i1", FirstName = "A", LastName = "A", Email = "a@x.test", Status = "Active" },
                new MemberEntity { Id = "m2", InstitutionId = "i1", FirstName = "B", LastName = "B", Email = "b@x.test", Status = "Active" },
                new MemberEntity { Id = "m3", InstitutionId = "i1", FirstName = "C", LastName = "C", Email = "c@x.test", Status = "Pending" },
                new Community { Id = "g1", InstitutionId = "i1", Name = "Accra Chapter" },
                new Community { Id = "g2", InstitutionId = "i1", Name = "Closed", IsActive = false },
                new Batch { Id = "b1", InstitutionId = "i1", Name = "Class of 2020", Year = 2020 },
                new AlumniEvent { Id = "e1", InstitutionId = "i1", Title = "Past", StartDate = DateTime.UtcNow.AddDays(-10), Status = "Completed" },
                new AlumniEvent { Id = "e2", InstitutionId = "i1", Title = "Called off", StartDate = DateTime.UtcNow.AddDays(-9), Status = "Cancelled" },
                new AlumniEvent { Id = "e3", InstitutionId = "i1", Title = "Next month", StartDate = DateTime.UtcNow.AddDays(30), Status = "Upcoming" },
                new Contribution { InstitutionId = "i1", CampaignId = "c", MemberId = "m1", Status = "Successful", PaymentMethod = "Paystack", Amount = 100 },
                new Contribution { InstitutionId = "i1", CampaignId = "c", MemberId = "m2", Status = "Failed", PaymentMethod = "Paystack", Amount = 900 });
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name);
        var audit = new Mock<IAuditLogService>();
        var stats = new ShowcaseStatsReader(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<Contribution>(ctx));
        return (new ShowcaseService(new AlumniPgRepository<Institution>(ctx), stats, audit.Object, new ConfigurationBuilder().Build(), NullLogger<ShowcaseService>.Instance), audit, name);
    }

    private static SaveShowcaseRequest Feature(bool permission = true, string? blurb = "Runs its chapters here", params string[] stats)
        => new(true, blurb, 1, stats.ToList(), permission);

    [Fact]
    public async Task The_numbers_are_counted_from_the_institutions_own_data()
    {
        var (service, _, _) = Create();

        var umat = (await service.GetAsync()).Data!.Single(e => e.InstitutionId == "i1");

        // Two active members (not the pending one); an active group, an active year group (not the closed group); one event held
        // (not the one called off or the one still to come); only the successful contribution.
        Assert.Equal((2, 2, 1, 100m), (umat.Numbers.Members, umat.Numbers.Groups, umat.Numbers.Events, umat.Numbers.Raised));
    }

    [Fact]
    public async Task Featuring_needs_the_confirmation_that_the_institution_agreed()
    {
        var (service, audit, _) = Create();

        var refused = await service.SaveAsync("i1", Feature(permission: false), "s1", "Staff");

        Assert.Equal(400, refused.Code);
        Assert.False((await service.GetAsync()).Data!.Single(e => e.InstitutionId == "i1").Featured);
        audit.Verify(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Only_an_active_institution_can_be_featured()
    {
        var (service, _, _) = Create();

        Assert.Equal(400, (await service.SaveAsync("i2", Feature(), "s1", "Staff")).Code);
    }

    [Fact]
    public async Task A_description_over_the_limit_and_a_number_that_cannot_be_shown_are_refused()
    {
        var (service, _, _) = Create();

        Assert.Equal(400, (await service.SaveAsync("i1", Feature(blurb: new string('x', ShowcaseService.BlurbMaxLength + 1)), "s1", "Staff")).Code);
        Assert.Equal(400, (await service.SaveAsync("i1", Feature(stats: ["members", "emails"]), "s1", "Staff")).Code);
    }

    [Fact]
    public async Task Featuring_saves_who_did_it_and_when_and_takes_it_back_cleanly()
    {
        var (service, audit, _) = Create();

        var saved = (await service.SaveAsync("i1", Feature(stats: ["members", "MEMBERS", "groups"]), "s1", "Efua Mensah")).Data!;

        Assert.True(saved.Featured);
        Assert.Equal(["members", "groups"], saved.Stats);   // normalised, no duplicates
        Assert.Equal("Efua Mensah", saved.FeaturedBy);
        Assert.NotNull(saved.FeaturedAt);
        audit.Verify(a => a.LogAsync("s1", "Efua Mensah", "featured institution on the website", "UMaT"), Times.Once);

        var removed = (await service.SaveAsync("i1", new SaveShowcaseRequest(false, null, 0, [], false), "s1", "Efua Mensah")).Data!;
        Assert.False(removed.Featured);
        Assert.Null(removed.FeaturedAt);
        audit.Verify(a => a.LogAsync("s1", "Efua Mensah", "removed institution from the website", "UMaT"), Times.Once);
    }

    [Fact]
    public async Task Saving_again_without_changing_whether_it_is_featured_does_not_log_or_reset_the_date()
    {
        var (service, audit, _) = Create();
        var first = (await service.SaveAsync("i1", Feature(), "s1", "Efua Mensah")).Data!;

        var second = (await service.SaveAsync("i1", Feature(blurb: "A new line"), "s2", "Someone Else")).Data!;

        Assert.Equal(first.FeaturedAt, second.FeaturedAt);
        Assert.Equal("Efua Mensah", second.FeaturedBy);
        audit.Verify(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}

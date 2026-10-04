using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Xunit;

using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class InstitutionActivationServiceTests
{
    // A Wednesday, so "this week" started on Monday 2026-09-21.
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThisWeek = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);

    private static AlumniDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AlumniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Mock.Of<ICurrentTenantService>());

    private static InstitutionActivationService NewService(AlumniDbContext db) => new(
        new AlumniPgRepository<InstitutionStaff>(db),
        new AlumniPgRepository<MemberEntity>(db),
        new AlumniPgRepository<Campaign>(db),
        new AlumniPgRepository<Contribution>(db),
        new AlumniPgRepository<StoreOrder>(db),
        new AlumniPgRepository<ServiceRequest>(db),
        new AlumniPgRepository<StaffActivityWeek>(db),
        new AlumniPgRepository<InstitutionActivitySnapshot>(db));

    private static InstitutionEntity FullyBrandedInstitution() => new()
    {
        Id = "inst-1",
        Name = "Test OSA",
        Slug = "test",
        LogoUrl = "https://cdn/logo.png",
        HeroImageUrls = ["https://cdn/hero.jpg"],
        LandingPageStories = [new LandingPageStory()],
        PayoutStatus = "Approved",
        OnboardedAt = Now.AddDays(-30),
    };

    private static void SeedActiveInstitution(AlumniDbContext db, string institutionId, int weeksOfStaffHistory)
    {
        for (var i = 0; i < 120; i++)
        {
            db.Members.Add(new MemberEntity
            {
                InstitutionId = institutionId,
                Status = "Active",
                LastLoginAt = i < 40 ? Now.AddDays(-3) : null,
            });
        }
        db.Campaigns.Add(new Campaign { InstitutionId = institutionId, Status = CampaignStatus.Active });
        db.Contributions.Add(new Contribution { InstitutionId = institutionId, Status = "Successful", PaymentMethod = "Paystack" });
        db.InstitutionStaff.Add(new InstitutionStaff { InstitutionId = institutionId, LastLoginAt = Now.AddDays(-1) });
        db.InstitutionStaff.Add(new InstitutionStaff { InstitutionId = institutionId, LastLoginAt = Now.AddDays(-2) });
        for (var w = 1; w <= weeksOfStaffHistory; w++)
            db.InstitutionActivitySnapshots.Add(new InstitutionActivitySnapshot { InstitutionId = institutionId, WeekStart = ThisWeek.AddDays(-7 * w), ActiveStaffCount = 2 });
        db.SaveChanges();
    }

    [Theory]
    [InlineData("2026-09-21T00:00:00Z", "2026-09-21")] // Monday midnight is its own week
    [InlineData("2026-09-23T15:30:00Z", "2026-09-21")] // Wednesday
    [InlineData("2026-09-27T23:59:00Z", "2026-09-21")] // Sunday still belongs to the Monday before
    public void WeekStart_ReturnsMondayMidnightUtc(string input, string expected)
    {
        var result = InstitutionActivationService.WeekStart(DateTime.Parse(input).ToUniversalTime());
        Assert.Equal(DateTime.Parse(expected), result.Date);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_AllCriteriaMet_WhenInstitutionIsFullyActive()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 2);

        var result = (await NewService(db).EvaluateAsync([FullyBrandedInstitution()], Now)).Single();

        Assert.True(result.AllMet, string.Join("; ", result.Criteria.Where(c => !c.Met).Select(c => $"{c.Key}: {c.Detail}")));
        Assert.Null(result.NextStep);
        Assert.False(result.IsStalled);
    }

    [Fact]
    public async Task EvaluateAsync_StaffStreakFails_WithOnlyTwoWeeksOfActivity()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 1);

        var result = (await NewService(db).EvaluateAsync([FullyBrandedInstitution()], Now)).Single();

        var staff = result.Criteria.Single(c => c.Key == InstitutionActivationService.StaffActivity);
        Assert.False(staff.Met);
        Assert.Contains("2/3 week streak", staff.Detail);
        Assert.Equal(InstitutionActivationService.StaffActivity, result.NextStepKey);
    }

    [Fact]
    public async Task EvaluateAsync_QuietStartToWeek_DoesNotBreakStreakEndingLastWeek()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 3);
        // Nobody has logged in yet this week.
        foreach (var s in db.InstitutionStaff.IgnoreQueryFilters()) s.LastLoginAt = ThisWeek.AddDays(-2);
        db.SaveChanges();

        var result = (await NewService(db).EvaluateAsync([FullyBrandedInstitution()], Now)).Single();

        Assert.True(result.Criteria.Single(c => c.Key == InstitutionActivationService.StaffActivity).Met);
    }

    [Fact]
    public async Task EvaluateAsync_ManualPaymentsDoNotCountAsOnline()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 2);
        db.Contributions.IgnoreQueryFilters().Single().PaymentMethod = "Manual";
        db.SaveChanges();

        var result = (await NewService(db).EvaluateAsync([FullyBrandedInstitution()], Now)).Single();

        Assert.False(result.Criteria.Single(c => c.Key == InstitutionActivationService.Payments).Met);
    }

    [Fact]
    public async Task EvaluateAsync_NewInstitution_NextStepIsBrandingAndStalledOnlyAfterTwoWeeks()
    {
        using var db = NewContext();
        var fresh = new InstitutionEntity { Id = "inst-2", Name = "New OSA", Slug = "new", OnboardedAt = Now.AddDays(-5) };
        var old = new InstitutionEntity { Id = "inst-3", Name = "Old OSA", Slug = "old", OnboardedAt = Now.AddDays(-20) };

        var results = await NewService(db).EvaluateAsync([fresh, old], Now);

        var freshResult = results.Single(r => r.InstitutionId == "inst-2");
        Assert.Equal(InstitutionActivationService.Branding, freshResult.NextStepKey);
        Assert.Equal(0, freshResult.MetCount);
        Assert.False(freshResult.IsStalled);
        Assert.True(results.Single(r => r.InstitutionId == "inst-3").IsStalled);
    }

    [Fact]
    public async Task EvaluateAsync_UsesInstitutionMemberThresholdOverride()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 2);
        var institution = FullyBrandedInstitution();
        institution.ActivationMinMembers = 200;

        var result = (await NewService(db).EvaluateAsync([institution], Now)).Single();

        var members = result.Criteria.Single(c => c.Key == InstitutionActivationService.Members);
        Assert.False(members.Met);
        Assert.StartsWith("120/200 members", members.Detail);
        Assert.Equal(200, result.MinMembers);
    }

    [Fact]
    public async Task EvaluateAsync_PendingApprovalsBecomeTheNextStep()
    {
        using var db = NewContext();
        var institution = FullyBrandedInstitution();
        for (var i = 0; i < 30; i++)
            db.Members.Add(new MemberEntity { InstitutionId = institution.Id, Status = "Pending" });
        db.SaveChanges();

        var result = (await NewService(db).EvaluateAsync([institution], Now)).Single();

        Assert.Equal(InstitutionActivationService.Members, result.NextStepKey);
        Assert.Contains("30 members are waiting for approval", result.NextStep);
        Assert.Contains("30 awaiting approval", result.Criteria.Single(c => c.Key == InstitutionActivationService.Members).Detail);
    }

    [Fact]
    public async Task EvaluateAsync_StoreOrderCountsAsOnlinePayment()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 2);
        db.Contributions.IgnoreQueryFilters().Single().PaymentMethod = "Manual";
        db.StoreOrders.Add(new StoreOrder { InstitutionId = "inst-1", Status = "Successful", PaymentMethod = "Paystack" });
        db.SaveChanges();

        var result = (await NewService(db).EvaluateAsync([FullyBrandedInstitution()], Now)).Single();

        Assert.True(result.Criteria.Single(c => c.Key == InstitutionActivationService.Payments).Met);
    }

    [Fact]
    public async Task EvaluateAsync_StaffActivityWeeksAloneSatisfyTheStreak()
    {
        using var db = NewContext();
        SeedActiveInstitution(db, "inst-1", weeksOfStaffHistory: 0);
        // No snapshots at all (worker never ran) — only sign-in records.
        foreach (var w in new[] { 0, 1, 2 })
            foreach (var staffId in new[] { "s1", "s2" })
                db.StaffActivityWeeks.Add(new StaffActivityWeek { InstitutionId = "inst-1", StaffId = staffId, WeekStart = ThisWeek.AddDays(-7 * w) });
        db.SaveChanges();

        var result = (await NewService(db).EvaluateAsync([FullyBrandedInstitution()], Now)).Single();

        Assert.True(result.Criteria.Single(c => c.Key == InstitutionActivationService.StaffActivity).Met);
    }

    [Fact]
    public async Task EvaluateAsync_FlagsOverduePastThirtyDayWindow()
    {
        using var db = NewContext();
        var onTime = new InstitutionEntity { Id = "a", Name = "A", Slug = "a", OnboardedAt = Now.AddDays(-25) };
        var late = new InstitutionEntity { Id = "b", Name = "B", Slug = "b", OnboardedAt = Now.AddDays(-31) };

        var results = await NewService(db).EvaluateAsync([onTime, late], Now);

        Assert.False(results.Single(r => r.InstitutionId == "a").IsOverdue);
        Assert.True(results.Single(r => r.InstitutionId == "b").IsOverdue);
    }

    [Fact]
    public async Task StaffActivityRecorder_WritesOneRowPerStaffPerWeek()
    {
        using var db = NewContext();
        var recorder = new StaffActivityRecorder(new AlumniPgRepository<StaffActivityWeek>(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StaffActivityRecorder>.Instance);

        await recorder.RecordAsync("inst-1", "staff-1", Now);
        await recorder.RecordAsync("inst-1", "staff-1", Now.AddDays(2)); // same week (Friday)
        await recorder.RecordAsync("inst-1", "staff-1", Now.AddDays(7)); // next week

        var weeks = db.StaffActivityWeeks.OrderBy(a => a.WeekStart).Select(a => a.WeekStart).ToList();
        Assert.Equal([ThisWeek, ThisWeek.AddDays(7)], weeks);
    }

    [Fact]
    public void StampStage_BackfillsSkippedStagesWithoutMovingExistingOnes()
    {
        var contacted = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var lead = new OnboardingLead { ContactedAt = contacted };

        OnboardingLeadStatuses.StampStage(lead, OnboardingLeadStatuses.Trial, Now);

        Assert.Equal(contacted, lead.ContactedAt);
        Assert.Equal(Now, lead.DemoBookedAt);
        Assert.Equal(Now, lead.TrialStartedAt);
        Assert.Null(lead.ApprovedAt);
    }
}

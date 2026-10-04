using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Xunit;

using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>
/// The Operations Worker's activation activities against an in-memory database.
/// The institution repository is mocked because EF's in-memory provider can't run
/// ExecuteUpdateAsync (used to stamp ActivatedAt / LastActivationNudgeSentAt
/// without a full-row write); those calls are verified instead. Email goes through
/// Temporal, which is reported unavailable here, so delivery is asserted via the
/// in-app notifications and SMS calls made alongside it.
/// </summary>
public class InstitutionActivationActivitiesTests
{
    private readonly CurrentTenantService tenant = new();
    private readonly AlumniDbContext db;
    private readonly Mock<IAlumniPgRepository<InstitutionEntity>> institutionRepo = new();
    private readonly Mock<ISmsService> sms = new();
    private readonly List<InstitutionEntity> institutions = [];

    public InstitutionActivationActivitiesTests()
    {
        db = new AlumniDbContext(
            new DbContextOptionsBuilder<AlumniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        institutionRepo
            .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync((Expression<Func<InstitutionEntity, bool>> p, bool _) => institutions.Where(p.Compile()).ToList());
        institutionRepo
            .Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync((Expression<Func<InstitutionEntity, bool>> p, bool _) => institutions.FirstOrDefault(p.Compile()));
        institutionRepo
            .Setup(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(),
                It.IsAny<Expression<Func<SetPropertyCalls<InstitutionEntity>, SetPropertyCalls<InstitutionEntity>>>>(), It.IsAny<bool>()))
            .ReturnsAsync(1);
        sms.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private InstitutionActivationActivities NewActivities() => new(
        institutionRepo.Object,
        new AlumniPgRepository<InstitutionStaff>(db),
        new AlumniPgRepository<MemberEntity>(db),
        new AlumniPgRepository<Contribution>(db),
        new AlumniPgRepository<StaffActivityWeek>(db),
        new AlumniPgRepository<InstitutionActivitySnapshot>(db),
        new AlumniPgRepository<Notification>(db),
        new AlumniPgRepository<PlatformStaff>(db),
        new AlumniPgRepository<PlatformNotification>(db),
        new AlumniPgRepository<OnboardingLead>(db),
        new InstitutionActivationService(
            new AlumniPgRepository<InstitutionStaff>(db),
            new AlumniPgRepository<MemberEntity>(db),
            new AlumniPgRepository<Campaign>(db),
            new AlumniPgRepository<Contribution>(db),
            new AlumniPgRepository<StoreOrder>(db),
            new AlumniPgRepository<ServiceRequest>(db),
            new AlumniPgRepository<StaffActivityWeek>(db),
            new AlumniPgRepository<InstitutionActivitySnapshot>(db)),
        tenant,
        Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
        sms.Object,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminPortalBaseDomain"] = "admin.example" }).Build(),
        NullLogger<InstitutionActivationActivities>.Instance);

    private static ActivationRunItem Item(Action<ActivationRunItemBuilder>? configure = null)
    {
        var b = new ActivationRunItemBuilder();
        configure?.Invoke(b);
        return new ActivationRunItem
        {
            InstitutionId = "inst-1", Name = "Test OSA", DaysLive = b.DaysLive, MetCount = 1, TotalCriteria = 5,
            IsActivated = b.IsActivated, IsStalled = b.IsStalled, IsOverdue = b.IsOverdue,
            NextStepKey = b.NextStepKey, NextStepLabel = "Payouts live", NextStep = b.NextStep,
            PayoutPending = b.PayoutPending, LastNudgeSentAt = b.LastNudgeSentAt, SetupNudgesEnabled = b.SetupNudgesEnabled,
            TrialEndsAt = b.TrialEndsAt,
        };
    }

    private sealed class ActivationRunItemBuilder
    {
        public int DaysLive = 10;
        public bool IsActivated, IsStalled, IsOverdue, PayoutPending;
        public bool SetupNudgesEnabled = true;
        public string? NextStepKey = InstitutionActivationService.Payouts;
        public string? NextStep = "Submit your bank details.";
        public DateTime? LastNudgeSentAt, TrialEndsAt;
    }

    [Theory]
    [InlineData(1, false)]   // inside the grace period
    [InlineData(10, true)]
    [InlineData(91, false)]  // past the nudge window
    public void IsNudgeDue_RespectsGraceAndWindow(int daysLive, bool expected)
    {
        Assert.Equal(expected, InstitutionActivationActivities.IsNudgeDue(Item(b => b.DaysLive = daysLive), DateTime.UtcNow));
    }

    [Fact]
    public void IsNudgeDue_FalseDuringCooldown_WhenMuted_WhenActivated_OrPayoutAwaitingReview()
    {
        var now = DateTime.UtcNow;
        Assert.False(InstitutionActivationActivities.IsNudgeDue(Item(b => b.LastNudgeSentAt = now.AddDays(-3)), now));
        Assert.True(InstitutionActivationActivities.IsNudgeDue(Item(b => b.LastNudgeSentAt = now.AddDays(-8)), now));
        Assert.False(InstitutionActivationActivities.IsNudgeDue(Item(b => b.SetupNudgesEnabled = false), now));
        Assert.False(InstitutionActivationActivities.IsNudgeDue(Item(b => b.IsActivated = true), now));
        Assert.False(InstitutionActivationActivities.IsNudgeDue(Item(b => b.PayoutPending = true), now));
    }

    [Fact]
    public void BuildDigest_ListsOverdueStalledAndEndingTrials()
    {
        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var items = new List<ActivationRunItem>
        {
            Item(b => { b.IsActivated = true; b.NextStep = null; b.NextStepKey = null; }),
            Item(b => { b.IsOverdue = true; b.IsStalled = true; b.DaysLive = 35; }),
            Item(b => { b.IsStalled = true; b.DaysLive = 20; b.PayoutPending = true; }),
            Item(b => { b.DaysLive = 5; b.TrialEndsAt = now.Date.AddDays(3); }),
        };

        var (title, body) = InstitutionActivationActivities.BuildDigest(items, now);

        Assert.Equal("Activation this week: 1 of 4 institutions active", title);
        Assert.Contains("Past the 30-day window: Test OSA (35 days, stuck on payouts live)", body);
        Assert.Contains("Live over 14 days and not there yet: Test OSA (20 days, payout details awaiting our review)", body);
        Assert.Contains("Trials ending within 7 days: Test OSA (8 Oct)", body);
    }

    [Fact]
    public async Task SendNudgeAsync_NotifiesSuperAdminsOnly_TextsThoseWithPhones_AndStampsCooldown()
    {
        institutions.Add(new InstitutionEntity { Id = "inst-1", Name = "Test OSA", Slug = "test", SmsNotificationsEnabled = true });
        db.InstitutionStaff.AddRange(
            new InstitutionStaff { Id = "sa-1", InstitutionId = "inst-1", Role = "SuperAdmin", Email = "a@x.org", FirstName = "Ama", Phone = "+233200000001" },
            new InstitutionStaff { Id = "sa-2", InstitutionId = "inst-1", Role = "SuperAdmin", Email = "b@x.org", FirstName = "Kofi" },
            new InstitutionStaff { Id = "ad-1", InstitutionId = "inst-1", Role = "Admin", Email = "c@x.org", FirstName = "Esi", Phone = "+233200000002" });
        db.SaveChanges();

        var sent = await NewActivities().SendNudgeAsync(Item());

        Assert.True(sent);
        var notified = db.Notifications.IgnoreQueryFilters().Where(n => n.Type == "SetupNudge").Select(n => n.RecipientId).OrderBy(x => x).ToList();
        Assert.Equal(["sa-1", "sa-2"], notified);
        Assert.All(db.Notifications.IgnoreQueryFilters(), n => Assert.Equal("inst-1", n.InstitutionId));
        sms.Verify(s => s.SendSmsAsync("+233200000001", It.Is<string>(m => m.Contains("https://test.admin.example")), It.IsAny<CancellationToken>()), Times.Once);
        sms.Verify(s => s.SendSmsAsync("+233200000002", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        institutionRepo.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(),
            It.IsAny<Expression<Func<SetPropertyCalls<InstitutionEntity>, SetPropertyCalls<InstitutionEntity>>>>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task SendNudgeAsync_SkipsSms_WhenInstitutionSmsIsOff()
    {
        institutions.Add(new InstitutionEntity { Id = "inst-1", Name = "Test OSA", Slug = "test", SmsNotificationsEnabled = false });
        db.InstitutionStaff.Add(new InstitutionStaff { InstitutionId = "inst-1", Role = "SuperAdmin", Email = "a@x.org", Phone = "+233200000001" });
        db.SaveChanges();

        Assert.True(await NewActivities().SendNudgeAsync(Item()));
        sms.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAllAsync_StampsActivatedAt_OnlyForNewlyActivatedInstitutions()
    {
        institutions.Add(new InstitutionEntity { Id = "inst-1", Name = "Not there", Slug = "a", Status = "Active", OnboardedAt = DateTime.UtcNow.AddDays(-20) });

        var items = await NewActivities().EvaluateAllAsync();

        var item = Assert.Single(items);
        Assert.False(item.IsActivated);
        Assert.True(item.IsStalled);
        Assert.Equal(InstitutionActivationService.Branding, item.NextStepKey);
        institutionRepo.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(),
            It.IsAny<Expression<Func<SetPropertyCalls<InstitutionEntity>, SetPropertyCalls<InstitutionEntity>>>>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SendFollowUpRemindersAsync_RemindsOwnerOnce_AndIgnoresFutureAndClosedLeads()
    {
        var today = DateTime.UtcNow.Date;
        db.PlatformStaff.AddRange(
            new PlatformStaff { Id = "owner", Name = "Owner", Role = "Sales" },
            new PlatformStaff { Id = "boss", Name = "Boss", Role = "SuperAdmin" });
        db.OnboardingLeads.AddRange(
            new OnboardingLead { Id = "due", InstitutionName = "Due OSA", ContactName = "C", Status = "Contacted", AssigneeStaffId = "owner", NextFollowUpAt = today },
            new OnboardingLead { Id = "unowned", InstitutionName = "Unowned OSA", ContactName = "C", Status = "Trial", NextFollowUpAt = today.AddDays(-2) },
            new OnboardingLead { Id = "future", InstitutionName = "Later OSA", ContactName = "C", Status = "Contacted", NextFollowUpAt = today.AddDays(3) },
            new OnboardingLead { Id = "closed", InstitutionName = "Closed OSA", ContactName = "C", Status = "Rejected", NextFollowUpAt = today });
        db.SaveChanges();
        var activities = NewActivities();

        Assert.Equal(2, await activities.SendFollowUpRemindersAsync());
        Assert.Equal(0, await activities.SendFollowUpRemindersAsync()); // already reminded

        var notes = db.PlatformNotifications.Where(n => n.Type == "LeadFollowUp").ToList();
        Assert.Single(notes, n => n.RelatedEntityId == "due" && n.RecipientStaffId == "owner");
        // No owner → every SuperAdmin/Sales staffer.
        Assert.Equal(["boss", "owner"], notes.Where(n => n.RelatedEntityId == "unowned").Select(n => n.RecipientStaffId).OrderBy(x => x));
        Assert.Contains("2 days ago", notes.First(n => n.RelatedEntityId == "unowned").Body);
    }

    [Fact]
    public async Task RecordActivitySnapshotAsync_UpsertsThisWeeksRow()
    {
        var now = DateTime.UtcNow;
        db.StaffActivityWeeks.AddRange(
            new StaffActivityWeek { InstitutionId = "inst-1", StaffId = "s1", WeekStart = InstitutionActivationService.WeekStart(now) },
            new StaffActivityWeek { InstitutionId = "inst-1", StaffId = "s2", WeekStart = InstitutionActivationService.WeekStart(now) });
        db.Members.Add(new MemberEntity { InstitutionId = "inst-1", Status = "Active", LastLoginAt = now });
        db.SaveChanges();
        var activities = NewActivities();

        await activities.RecordActivitySnapshotAsync("inst-1");
        await activities.RecordActivitySnapshotAsync("inst-1");

        var row = Assert.Single(db.InstitutionActivitySnapshots);
        Assert.Equal(2, row.ActiveStaffCount);
        Assert.Equal(1, row.MemberCount);
        Assert.Equal(1, row.MembersActiveThisWeek);
    }
}

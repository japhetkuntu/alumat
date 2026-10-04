using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class AnalyticsServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly int ThisYear = Now.Year;
    private static readonly AuthData Super = new() { Id = "super", Role = "SuperAdmin" };

    private static async Task<AnalyticsService> Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in seed) { if (e is ITenantScoped t && string.IsNullOrEmpty(t.InstitutionId)) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
        }
        var ctx = TestDb.Create(name, Tenant);
        return new AnalyticsService(
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Department>(ctx), new AlumniPgRepository<CommunityMembership>(ctx),
            new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx),
            new AlumniPgRepository<ServiceRequest>(ctx), new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<ForumThread>(ctx),
            new AlumniPgRepository<ForumPost>(ctx), TestDb.Tenant(Tenant), new InMemoryRedisService<InstitutionRedisConfig>(), NullLogger<AnalyticsService>.Instance);
    }

    private static async Task<InstitutionAnalyticsDto> Run(AnalyticsService service, AuthData? admin = null, params string[] disabled)
        => (await service.GetAnalyticsAsync(admin ?? Super, disabled)).Data!;

    private static int next;
    private static MemberEntity Person(int year = 2014, string status = "Active", DateTime? joined = null, DateTime? lastLogin = null,
        string department = "mining", string? location = null, string? id = null) => new()
    {
        Id = id ?? $"m{Interlocked.Increment(ref next)}", GraduationYear = year, Status = status, DepartmentId = department, Location = location,
        FirstName = "A", LastName = "B", Email = $"{Guid.NewGuid():N}@x.com", CreatedAt = joined ?? Now.AddYears(-3), LastLoginAt = lastLogin,
    };

    private static Contribution Paid(string memberId, string campaignId, decimal amount, DateTime? on = null, string status = "Successful") => new()
    {
        MemberId = memberId, CampaignId = campaignId, Amount = amount, Status = status, ConfirmedAt = on ?? Now.AddDays(-1), CreatedAt = on ?? Now.AddDays(-1),
    };

    // ── Members and growth ──────────────────────────────────────────────────

    [Fact]
    public async Task Participation_counts_only_approved_members_and_separates_ever_from_recently()
    {
        var a = await Run(await Create(
            Person(lastLogin: Now.AddDays(-2)), Person(lastLogin: Now.AddDays(-90)), Person(), Person(status: "Pending", lastLogin: Now), Person(status: "Suspended")));

        Assert.Equal(new AnalyticsMembersDto(Total: 5, Approved: 3, Pending: 1, SignedInEver: 2, SignedInLast30Days: 1), a.Members);
    }

    [Fact]
    public async Task Growth_is_twelve_calendar_months_ending_this_month_with_empty_months_kept_and_a_running_total()
    {
        var a = await Run(await Create(Person(joined: Now.AddYears(-5)), Person(joined: Now.AddYears(-5)), Person(joined: Now), Person(joined: Now)));

        Assert.Equal(12, a.Growth.Count);
        Assert.Equal((Now.Year, Now.Month, 2, 4), (a.Growth[^1].Year, a.Growth[^1].Month, a.Growth[^1].Count, a.Growth[^1].Total));
        Assert.Equal((0, 2), (a.Growth[0].Count, a.Growth[0].Total));
        Assert.All(a.Growth.Take(11), m => Assert.Equal(0, m.Count));
    }

    // ── Dues ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dues_standing_is_paid_over_eligible_broken_down_by_year_group()
    {
        var a = await Run(await Create(
            new Campaign { Id = "dues", IsMembershipCampaign = true, MembershipYear = ThisYear, Title = "Dues" },
            Person(2010, id: "a"), Person(2010, id: "b"), Person(2015, id: "c"), Person(2015, status: "Pending", id: "not-approved"),
            Paid("a", "dues", 100), Paid("a", "dues", 50), Paid("c", "dues", 100), Paid("b", "dues", 100, status: "Pending"), Paid("not-approved", "dues", 100)));

        Assert.NotNull(a.Dues);
        Assert.Equal((ThisYear, 3, 2), (a.Dues!.Year, a.Dues.Eligible, a.Dues.Paid));
        Assert.Equal([new AnalyticsDuesYearGroupDto(2010, 2, 1), new AnalyticsDuesYearGroupDto(2015, 1, 1)], a.Dues.ByYearGroup);
    }

    [Fact]
    public async Task Dues_aimed_at_some_year_groups_only_count_those_members_as_eligible()
    {
        var a = await Run(await Create(
            new Campaign { IsMembershipCampaign = true, MembershipYear = ThisYear, Title = "Dues", YearGroups = [2010] }, Person(2010), Person(2015)));

        Assert.Equal(1, a.Dues!.Eligible);
    }

    [Fact]
    public async Task With_no_dues_set_this_year_or_contributions_switched_off_there_is_no_dues_section()
    {
        Assert.Null((await Run(await Create(Person(), new Campaign { IsMembershipCampaign = true, MembershipYear = ThisYear - 1, Title = "Last year" }))).Dues);
        Assert.Null((await Run(await Create(Person(), new Campaign { IsMembershipCampaign = true, MembershipYear = ThisYear, Title = "Dues" }), null, InstitutionFeatures.Contributions)).Dues);
    }

    // ── Money ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Money_compares_this_year_so_far_with_the_same_stretch_of_last_year()
    {
        var a = await Run(await Create(
            Paid("a", "c", 100), Paid("b", "c", 40), Paid("a", "c", 999, status: "Failed"),
            Paid("a", "c", 70, on: Now.AddYears(-1).AddDays(-1)),
            // Later last year than today's date: belongs to last year's total, but not to a like-for-like comparison.
            Paid("a", "c", 500, on: new DateTime(ThisYear - 1, 12, 31, 23, 0, 0, DateTimeKind.Utc))));

        Assert.Equal(2, a.Money!.PayersThisYear);
        Assert.Equal(12, a.Money.Months.Count);
        if (Now is { Month: 1, Day: 1 }) return; // "yesterday" is last year on the first day of the year
        Assert.Equal(140m, a.Money.ThisYear);
        if (Now is { Month: 12, Day: 31 }) return;
        Assert.Equal(70m, a.Money.LastYearToDate);
    }

    [Fact]
    public async Task Money_is_split_by_source_and_leaves_out_sources_that_are_switched_off()
    {
        var seed = new object[]
        {
            Paid("a", "c", 100),
            new StoreOrder { Status = "Successful", TotalAmount = 60, ConfirmedAt = Now, CreatedAt = Now },
            new ServiceRequest { PaymentStatus = "Successful", Amount = 25, ConfirmedAt = Now, CreatedAt = Now },
        };

        var all = (await Run(await Create(seed))).Money!.Months[^1];
        var noStore = (await Run(await Create(seed), null, InstitutionFeatures.Store)).Money!.Months[^1];

        Assert.Equal((60m, 25m), (all.Store, all.Services));
        Assert.Equal((0m, 25m), (noStore.Store, noStore.Services));
        Assert.Null((await Run(await Create(seed), null, InstitutionFeatures.Contributions, InstitutionFeatures.Store, InstitutionFeatures.Services)).Money);
    }

    // ── Composition ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Locations_typed_differently_are_counted_as_one_place()
    {
        var a = await Run(await Create(
            Person(location: "Accra"), Person(location: "accra "), Person(location: "ACCRA"), Person(location: "Accra"),
            Person(location: "Kumasi"), Person(location: null), Person(location: ""), Person(status: "Pending", location: "Tamale")));

        Assert.Equal([new AnalyticsSliceDto("Accra", 4), new AnalyticsSliceDto("Kumasi", 1)], a.Composition.ByLocation);
        Assert.Equal(2, a.Composition.WithoutLocation);
    }

    [Fact]
    public async Task Members_are_grouped_by_department_name_and_by_graduation_year_in_order()
    {
        var a = await Run(await Create(
            new Department { Id = "mining", Name = "Mining Engineering" }, new Department { Id = "geo", Name = "Geology" },
            Person(2015), Person(2010), Person(2010, department: "geo"), Person(0)));

        Assert.Equal([new AnalyticsSliceDto("Mining Engineering", 3), new AnalyticsSliceDto("Geology", 1)], a.Composition.ByDepartment);
        Assert.Equal([new AnalyticsSliceDto("2010", 2), new AnalyticsSliceDto("2015", 1)], a.Composition.ByYearGroup);
    }

    // ── Activity and scope ──────────────────────────────────────────────────

    [Fact]
    public async Task Recent_activity_sits_beside_the_thirty_days_before_it()
    {
        var a = await Run(await Create(
            Person(joined: Now.AddDays(-5)), Person(joined: Now.AddDays(-45)), Person(joined: Now.AddDays(-50)), Person(joined: Now.AddDays(-200)),
            new EventRsvp { MemberId = "x", Status = "Confirmed", CreatedAt = Now.AddDays(-3) }, new EventRsvp { MemberId = "x", Status = "Cancelled", CreatedAt = Now.AddDays(-3) },
            new ForumThread { AuthorId = "x", Title = "t", CreatedAt = Now.AddDays(-2) }, new ForumPost { AuthorId = "x", Content = "p", CreatedAt = Now.AddDays(-2) },
            new ForumPost { AuthorId = "x", Content = "p", CreatedAt = Now.AddDays(-40) }, new ForumPost { AuthorId = "x", Content = "gone", IsDeleted = true, CreatedAt = Now.AddDays(-2) }));

        Assert.Equal(new AnalyticsChangeDto(1, 2), a.Activity.NewMembers);
        Assert.Equal(new AnalyticsChangeDto(1, 0), a.Activity.EventSignUps);
        Assert.Equal(new AnalyticsChangeDto(2, 1), a.Activity.ForumPosts);
    }

    // The money features are switched off here because a scoped admin's campaign filter matches on an
    // integer-array column (Campaign.YearGroups), which Postgres can evaluate and the in-memory test
    // database cannot. The scoped money path uses the same predicate as ReportService's summary.
    [Fact]
    public async Task A_scoped_admin_sees_only_their_year_groups_and_their_communities_approved_members()
    {
        var scoped = new AuthData { Id = "scoped", Role = "ScopedAdmin", YearGroups = [2010], CommunityIds = ["c1"] };
        var service = await Create(
            Person(2010, id: "in-year", location: "Accra"), Person(2015, id: "in-community"), Person(2015, id: "outside", location: "Kumasi"),
            new CommunityMembership { CommunityId = "c1", MemberId = "in-community", Status = "Approved" },
            new CommunityMembership { CommunityId = "c1", MemberId = "outside", Status = "Pending" },
            new EventRsvp { MemberId = "in-year", Status = "Confirmed", CreatedAt = Now.AddDays(-3) },
            new EventRsvp { MemberId = "outside", Status = "Confirmed", CreatedAt = Now.AddDays(-3) });

        var a = await Run(service, scoped, InstitutionFeatures.Contributions, InstitutionFeatures.Store, InstitutionFeatures.Services);

        Assert.Equal(2, a.Members.Total);
        Assert.Equal([new AnalyticsSliceDto("Accra", 1)], a.Composition.ByLocation);
        Assert.Equal(1, a.Activity.EventSignUps.Last30Days);
        Assert.Null(a.Money);
    }

    [Fact]
    public async Task The_result_is_cached_per_scope_so_a_scoped_admin_never_gets_a_super_admins_figures()
    {
        var service = await Create(Person(2010), Person(2015));
        var scoped = new AuthData { Id = "scoped", Role = "ScopedAdmin", YearGroups = [2010], CommunityIds = [] };
        string[] moneyOff = [InstitutionFeatures.Contributions, InstitutionFeatures.Store, InstitutionFeatures.Services];

        Assert.Equal(2, (await Run(service, Super, moneyOff)).Members.Total);
        Assert.Equal(1, (await Run(service, scoped, moneyOff)).Members.Total);
        Assert.Equal(2, (await Run(service, Super, moneyOff)).Members.Total);
    }
}

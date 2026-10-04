using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Platform.Api.Tests;

public class PlatformAnalyticsServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static async Task<PlatformAnalyticsDto> Run(bool includeMoney, params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            db.AddRange(seed);
            await db.SaveChangesAsync();
        }
        var ctx = TestDb.Create(name);
        var service = new PlatformAnalyticsService(
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<Member>(ctx), new AlumniPgRepository<Contribution>(ctx),
            new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx), NullLogger<PlatformAnalyticsService>.Instance);
        return (await service.GetAnalyticsAsync(includeMoney)).Data!;
    }

    private static Institution Inst(string id, DateTime? onboarded = null, bool activated = false) => new()
    {
        Id = id, Name = id, Slug = id, OnboardedAt = onboarded ?? Now.AddYears(-2), ActivatedAt = activated ? Now.AddYears(-1) : null,
    };

    private static Member Person(string institution, DateTime? lastLogin = null, string status = "Active", DateTime? joined = null) => new()
    {
        InstitutionId = institution, Status = status, LastLoginAt = lastLogin, CreatedAt = joined ?? Now.AddYears(-2),
        FirstName = "A", LastName = "B", Email = $"{Guid.NewGuid():N}@x.com",
    };

    private static Contribution Paid(string institution, decimal amount, decimal earned = 0, DateTime? on = null, string status = "Successful") => new()
    {
        InstitutionId = institution, Amount = amount, PlatformRevenueAmount = earned, Status = status,
        ConfirmedAt = on ?? Now.AddDays(-1), CreatedAt = on ?? Now.AddDays(-1),
    };

    [Fact]
    public async Task Institutions_are_counted_as_activated_collecting_and_alive_across_every_tenant()
    {
        var a = await Run(true,
            Inst("alive", activated: true), Inst("paid-long-ago"), Inst("empty"),
            Person("alive", lastLogin: Now.AddDays(-2)), Person("alive", status: "Pending"), Person("paid-long-ago", lastLogin: Now.AddDays(-200)),
            Paid("alive", 100), Paid("paid-long-ago", 100, on: Now.AddDays(-200)));

        Assert.Equal(new PlatformAnalyticsInstitutionsDto(Total: 3, Activated: 1, Collecting: 1, WithRecentSignIns: 1), a.Institutions);
        Assert.Equal(new PlatformAnalyticsMembersDto(Total: 3, Approved: 2, SignedInLast30Days: 1), a.Members);
    }

    [Fact]
    public async Task Growth_runs_twelve_months_to_this_month_with_running_totals()
    {
        var a = await Run(true, Inst("old"), Inst("new", onboarded: Now), Person("old"), Person("new", joined: Now), Person("new", joined: Now));

        Assert.Equal(12, a.InstitutionGrowth.Count);
        Assert.Equal((Now.Month, 1, 2), (a.InstitutionGrowth[^1].Month, a.InstitutionGrowth[^1].Count, a.InstitutionGrowth[^1].Total));
        Assert.Equal((1, 1), (a.InstitutionGrowth[0].Total, a.MemberGrowth[0].Total));
        Assert.Equal((2, 3), (a.MemberGrowth[^1].Count, a.MemberGrowth[^1].Total));
    }

    [Fact]
    public async Task Leaders_are_ranked_by_what_they_collected_in_the_last_ninety_days_from_every_source()
    {
        var a = await Run(true,
            Inst("big"), Inst("small"), Inst("stale"), Person("big"),
            Paid("big", 300, earned: 6), Paid("big", 999, status: "Failed"),
            new StoreOrder { InstitutionId = "big", Status = "Successful", TotalAmount = 50, PlatformFeeAmount = 2, ConfirmedAt = Now, CreatedAt = Now },
            Paid("small", 80, earned: 1), Paid("stale", 5000, on: Now.AddDays(-120)));

        Assert.Equal(["big", "small"], a.Leaders.Select(l => l.Name));
        Assert.Equal((350m, 8m, 1), (a.Leaders[0].Collected, a.Leaders[0].Earned, a.Leaders[0].Members));
    }

    [Fact]
    public async Task Quiet_institutions_have_members_but_no_recent_sign_in_longest_silent_first()
    {
        var a = await Run(true,
            Inst("lively"), Inst("silent-60"), Inst("never"), Inst("no-members"),
            Person("lively", lastLogin: Now.AddDays(-1)), Person("silent-60", lastLogin: Now.AddDays(-60)), Person("silent-60", lastLogin: Now.AddDays(-90)), Person("never"),
            Paid("silent-60", 10, on: Now.AddDays(-70)));

        Assert.Equal(["never", "silent-60"], a.Quiet.Select(q => q.Name));
        Assert.Null(a.Quiet[0].LastSignIn);
        Assert.Equal(2, a.Quiet[1].Members);
        Assert.NotNull(a.Quiet[1].LastPayment);
    }

    [Fact]
    public async Task Roles_that_do_not_see_revenue_get_no_money_and_no_leaderboard_but_everything_else()
    {
        var a = await Run(false, Inst("big"), Person("big"), Paid("big", 300, earned: 6));

        Assert.Null(a.Money);
        Assert.Empty(a.Leaders);
        Assert.Equal(1, a.Institutions.Total);
        Assert.Equal(1, a.Institutions.Collecting);
    }

    [Fact]
    public async Task Money_shows_what_was_collected_and_what_we_earned_this_year_and_by_month()
    {
        var a = await Run(true, Inst("i"), Paid("i", 200, earned: 4), Paid("i", 100, earned: 2, on: Now.AddMonths(-14)));

        Assert.Equal(12, a.Money!.Months.Count);
        Assert.Equal(300m, a.Money.Months.Sum(m => m.Collected) + 100m);
        if (Now is { Month: 1, Day: 1 }) return; // "yesterday" is last year on the first day of the year
        Assert.Equal((200m, 4m), (a.Money.ThisYearCollected, a.Money.ThisYearEarned));
    }
}

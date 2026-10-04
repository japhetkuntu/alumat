using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class LeaderboardServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly int ThisYear = DateTime.UtcNow.Year;

    private static async Task<LeaderboardService> Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in seed) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
        }
        var ctx = TestDb.Create(name, Tenant);
        return new LeaderboardService(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<Campaign>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), NullLogger<LeaderboardService>.Instance);
    }

    private static MemberEntity Person(string id, int year, string status = "Active") => new() { Id = id, GraduationYear = year, Status = status, FirstName = id, Email = $"{id}@x.com" };

    [Fact]
    public async Task With_no_members_the_leaderboard_is_empty()
        => Assert.Empty((await (await Create()).GetLeaderboardAsync()).Data!);

    [Fact]
    public async Task Only_active_members_count_towards_a_year_groups_size()
    {
        var service = await Create(Person("a", 2015), Person("b", 2015), Person("c", 2015, "Pending"), Person("d", 2016, "Suspended"));

        var board = (await service.GetLeaderboardAsync()).Data!;

        var only = Assert.Single(board);
        Assert.Equal((2015, 2), (only.YearGroup, only.TotalMembers));
    }

    [Fact]
    public async Task The_membership_rate_is_current_year_dues_payers_over_active_members_to_one_decimal()
    {
        var service = await Create(
            Person("a", 2015), Person("b", 2015), Person("c", 2015),
            new Campaign { Id = "dues", IsMembershipCampaign = true, MembershipYear = ThisYear, Title = "Dues" },
            new Contribution { Id = "1", CampaignId = "dues", MemberId = "a", Status = "Successful", Amount = 100 });

        var entry = (await service.GetLeaderboardAsync()).Data!.Single();

        Assert.Equal((1, 33.3m), (entry.MembershipPaidCount, entry.MembershipRate));
    }

    [Fact]
    public async Task Only_successful_payments_for_the_current_years_dues_campaign_count_as_membership_paid()
    {
        var service = await Create(
            Person("a", 2015), Person("b", 2015), Person("c", 2015),
            new Campaign { Id = "dues-now", IsMembershipCampaign = true, MembershipYear = ThisYear, Title = "Now" },
            new Campaign { Id = "dues-old", IsMembershipCampaign = true, MembershipYear = ThisYear - 1, Title = "Old" },
            new Campaign { Id = "fund", IsMembershipCampaign = false, Title = "Fund" },
            new Contribution { Id = "1", CampaignId = "dues-now", MemberId = "a", Status = "Successful", Amount = 100 },
            new Contribution { Id = "2", CampaignId = "dues-now", MemberId = "b", Status = "Pending", Amount = 100 },
            new Contribution { Id = "3", CampaignId = "dues-old", MemberId = "c", Status = "Successful", Amount = 100 },
            new Contribution { Id = "4", CampaignId = "fund", MemberId = "c", Status = "Successful", Amount = 100 });

        var entry = (await service.GetLeaderboardAsync()).Data!.Single();

        Assert.Equal(1, entry.MembershipPaidCount);
    }

    [Fact]
    public async Task Total_contributed_sums_every_successful_payment_by_the_year_groups_members()
    {
        var service = await Create(
            Person("a", 2015), Person("b", 2016),
            new Contribution { Id = "1", CampaignId = "x", MemberId = "a", Status = "Successful", Amount = 100.50m },
            new Contribution { Id = "2", CampaignId = "y", MemberId = "a", Status = "Successful", Amount = 50 },
            new Contribution { Id = "3", CampaignId = "x", MemberId = "a", Status = "Failed", Amount = 999 },
            new Contribution { Id = "4", CampaignId = "x", MemberId = "b", Status = "Successful", Amount = 10 });

        var board = (await service.GetLeaderboardAsync()).Data!.ToDictionary(e => e.YearGroup);

        Assert.Equal((150.50m, 10m), (board[2015].TotalContributed, board[2016].TotalContributed));
    }

    [Fact]
    public async Task Event_attendance_counts_confirmed_rsvps_only()
    {
        var service = await Create(
            Person("a", 2015),
            new EventRsvp { Id = "1", EventId = "e1", MemberId = "a", Status = "Confirmed" },
            new EventRsvp { Id = "2", EventId = "e2", MemberId = "a", Status = "Confirmed" },
            new EventRsvp { Id = "3", EventId = "e3", MemberId = "a", Status = "Cancelled" });

        Assert.Equal(2, (await service.GetLeaderboardAsync()).Data!.Single().EventAttendanceCount);
    }

    [Fact]
    public async Task Year_groups_are_ranked_by_membership_rate_then_by_total_contributed()
    {
        var service = await Create(
            Person("a1", 2014), Person("a2", 2014),            // 0% paid, GHS 500 given
            Person("b1", 2015), Person("b2", 2015),            // 50% paid
            Person("c1", 2016), Person("c2", 2016),            // 0% paid, GHS 900 given
            new Campaign { Id = "dues", IsMembershipCampaign = true, MembershipYear = ThisYear, Title = "Dues" },
            new Contribution { Id = "1", CampaignId = "dues", MemberId = "b1", Status = "Successful", Amount = 10 },
            new Contribution { Id = "2", CampaignId = "fund", MemberId = "a1", Status = "Successful", Amount = 500 },
            new Contribution { Id = "3", CampaignId = "fund", MemberId = "c1", Status = "Successful", Amount = 900 });

        var order = (await service.GetLeaderboardAsync()).Data!.Select(e => e.YearGroup);

        Assert.Equal(new[] { 2015, 2016, 2014 }, order);
    }

    [Fact]
    public async Task A_year_group_with_no_activity_reports_zeros_not_missing_values()
    {
        var entry = (await (await Create(Person("a", 2015))).GetLeaderboardAsync()).Data!.Single();
        Assert.Equal((0, 0m, 0m, 0), (entry.MembershipPaidCount, entry.MembershipRate, entry.TotalContributed, entry.EventAttendanceCount));
    }
}

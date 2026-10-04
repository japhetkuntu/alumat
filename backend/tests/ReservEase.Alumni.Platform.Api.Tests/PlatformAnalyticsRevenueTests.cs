using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Tests;

public class PlatformAnalyticsRevenueTests
{
    private static async Task<PlatformAnalyticsService> Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            foreach (var e in seed) db.Add(e);
            await db.SaveChangesAsync();
        }
        var ctx = TestDb.Create(name);
        return new PlatformAnalyticsService(new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Contribution>(ctx),
            new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx), NullLogger<PlatformAnalyticsService>.Instance);
    }

    private static Contribution Paid(decimal amount, decimal platformFee, decimal revenue) => new()
    {
        Id = Guid.NewGuid().ToString("N"), InstitutionId = "i1", CampaignId = "c", MemberId = "m", Status = "Successful",
        Amount = amount, PlatformFeeAmount = platformFee, PlatformRevenueAmount = revenue, ConfirmedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task What_the_platform_earned_on_a_contribution_is_its_revenue_amount_not_the_fee_added_on_top_of_it()
    {
        // A GHS 100 payment with a 2.00 platform fee: the transaction charge routed to us was 4.00 and Paystack took 1.90,
        // so the recorded revenue is 2.10. The fee is already inside that figure, so adding it again would report 4.10.
        var service = await Create(new Institution { Id = "i1", Name = "UMaT", Slug = "umat", Status = "Active" }, Paid(100m, platformFee: 2.00m, revenue: 2.10m));

        var money = (await service.GetAnalyticsAsync(includeMoney: true)).Data!.Money!;

        Assert.Equal(100m, money.ThisYearCollected);
        Assert.Equal(2.10m, money.ThisYearEarned);
        Assert.Equal(2.10m, money.Months.Sum(m => m.Earned));
    }

    [Fact]
    public async Task Earnings_are_summed_across_payments_and_the_leaderboard_uses_the_same_figure()
    {
        var service = await Create(new Institution { Id = "i1", Name = "UMaT", Slug = "umat", Status = "Active" },
            Paid(100m, 2m, 2.1m), Paid(50m, 1m, 1.05m));

        var data = (await service.GetAnalyticsAsync(includeMoney: true)).Data!;

        Assert.Equal(150m, data.Money!.ThisYearCollected);
        Assert.Equal(3.15m, data.Money.ThisYearEarned);
        Assert.Equal(3.15m, data.Money.Months.Sum(m => m.Earned));
    }
}

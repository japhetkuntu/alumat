using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Platform.Api.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Microsoft.Extensions.Logging;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>
/// One of each kind of payment, settled into the main account, a year group's own account and a group's own account, as the platform
/// sees them. The platform must be able to see all of the money collected, wherever it was paid into, and what the platform earned on it.
/// </summary>
public class PaymentsAcrossAccountsTests
{
    private const string Inst = "i1";
    private static readonly DateTime Now = DateTime.UtcNow;

    // Collected: contributions 4 x 100 on Paystack plus 25 cash, tickets 30 + 50, a store order 40, a service request 20.
    private const decimal Everything = 425m + 80m + 40m + 20m;
    // Earned: 4 online contributions x 2, tickets 1 + 1, store 1, service 0.5. (Cash earns nothing.)
    private const decimal EarnedEverything = 8m + 2m + 1m + 0.5m;

    private static object[] Scenario(DateTime? confirmedAt = null)
    {
        var at = confirmedAt ?? Now.AddDays(-1);
        return
        [
            new Institution { Id = Inst, Name = "UMaT", Slug = "umat", Status = "Active", PaystackSubaccountCode = "ACCT_main", OnboardedAt = Now.AddYears(-2) },
            new Batch { Id = "b2020", InstitutionId = Inst, Name = "Class of 2020", Year = 2020, PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_year" },
            new Community { Id = "g1", InstitutionId = Inst, Name = "Accra Chapter", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_group" },
            new Community { Id = "g2", InstitutionId = Inst, Name = "Kumasi Chapter", PayoutStatus = "Pending" },
            new Campaign { Id = "cMain", InstitutionId = Inst, Title = "Annual dues" },
            new Campaign { Id = "cYear", InstitutionId = Inst, Title = "Class gift", YearGroups = [2020] },
            new Campaign { Id = "cGroup", InstitutionId = Inst, Title = "Chapter hall", CommunityId = "g1" },
            new Campaign { Id = "cPending", InstitutionId = Inst, Title = "Chapter outing", CommunityId = "g2" },
            Paid("cMain", 100, at), Paid("cYear", 100, at), Paid("cGroup", 100, at), Paid("cPending", 100, at),
            new Contribution { InstitutionId = Inst, CampaignId = "cMain", MemberId = "m2", Status = "Successful", PaymentMethod = "Cash", Amount = 25, ConfirmedAt = at, CreatedAt = at },
            new AlumniEvent { Id = "eMain", InstitutionId = Inst, Title = "Homecoming" },
            new AlumniEvent { Id = "eGroup", InstitutionId = Inst, Title = "Chapter dinner", CommunityId = "g1" },
            new EventRsvp { InstitutionId = Inst, EventId = "eMain", MemberId = "m1", Status = EventRsvpStatuses.Confirmed, AmountPaid = 30, PlatformFeeAmount = 1, PaidAt = at, CreatedAt = at, TransactionRef = "t1" },
            new EventRsvp { InstitutionId = Inst, EventId = "eGroup", MemberId = "m1", Status = EventRsvpStatuses.Confirmed, AmountPaid = 50, PlatformFeeAmount = 1, PaidAt = at, CreatedAt = at, TransactionRef = "t2" },
            new StoreOrder { Id = "o1", InstitutionId = Inst, Status = "Successful", PaymentMethod = "Paystack", TotalAmount = 40, PlatformFeeAmount = 1, ConfirmedAt = at, CreatedAt = at },
            new ServiceRequest { Id = "r1", InstitutionId = Inst, ServiceTypeId = "s1", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 20, PlatformFeeAmount = 0.5m, ConfirmedAt = at, CreatedAt = at },
        ];
    }

    private static Contribution Paid(string campaign, decimal amount, DateTime at) => new()
    {
        InstitutionId = Inst, CampaignId = campaign, MemberId = "m1", Status = "Successful", PaymentMethod = "Paystack", Amount = amount,
        PlatformRevenueAmount = 2, ConfirmedAt = at, CreatedAt = at,
    };

    private static string Seed(DateTime? confirmedAt = null)
    {
        var name = TestDb.NewName();
        using var db = TestDb.Create(name);
        foreach (var e in Scenario(confirmedAt)) db.Add(e);
        db.SaveChanges();
        return name;
    }

    private static InstitutionManagementService Management(string name)
    {
        var ctx = TestDb.Create(name);
        return new InstitutionManagementService(ctx,
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<InstitutionStaff>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx), new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<AlumniEvent>(ctx),
            new AlumniPgRepository<PaymentTransaction>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<ForumCategory>(ctx), new AlumniPgRepository<PlatformStaff>(ctx), Mock.Of<IAuditLogService>(), Mock.Of<IPaystackService>(),
            new ConfigurationBuilder().Build(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
            MsOptions.Create(new MailtrapConfig()), new Mock<IRedisService<PlatformRedisConfig>>().Object, new FixedScope(), NullLogger<InstitutionManagementService>.Instance,
            new PaymentAccountResolver(new AlumniPgRepository<Batch>(ctx), new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<ServiceType>(ctx)));
    }

    [Fact]
    public async Task The_institutions_revenue_page_counts_all_the_money_collected_whichever_account_it_settled_into()
    {
        var revenue = (await Management(Seed()).GetRevenueAsync(Inst)).Data!;

        Assert.Equal(Everything, revenue.GrossCollected);
        Assert.Equal(EarnedEverything, revenue.PlatformFeeTotal);
        Assert.Equal(9, revenue.ConfirmedPaymentCount);
    }

    [Fact]
    public async Task The_revenue_page_says_how_much_went_into_each_account_and_the_accounts_add_up_to_the_whole()
    {
        var revenue = (await Management(Seed()).GetRevenueAsync(Inst)).Data!;
        var accounts = revenue.ByAccount!;

        Assert.Equal(Everything, accounts.Sum(a => a.Amount));
        var main = Assert.Single(accounts, a => a.IsMain);
        Assert.Equal(Everything - 100m - 100m - 50m, main.Amount);   // the pending group's fundraiser stays with the institution
        Assert.Equal(100m, Assert.Single(accounts, a => a.Name == "Class of 2020").Amount);
        Assert.Equal(150m, Assert.Single(accounts, a => a.Name == "Accra Chapter").Amount);   // its fundraiser and its event's ticket
    }

    [Fact]
    public async Task The_platform_wide_analytics_count_all_the_money_and_all_the_earnings()
    {
        var name = Seed();
        var ctx = TestDb.Create(name);
        var service = new PlatformAnalyticsService(
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Contribution>(ctx),
            new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx), new AlumniPgRepository<EventRsvp>(ctx), NullLogger<PlatformAnalyticsService>.Instance);

        var money = (await service.GetAnalyticsAsync(true)).Data!.Money!;

        Assert.Equal(Everything, money.ThisYearCollected);
        Assert.Equal(EarnedEverything, money.ThisYearEarned);
    }

    [Fact]
    public async Task The_payments_list_has_a_row_for_every_payment_and_says_which_account_it_was_paid_into()
    {
        var rows = (await Management(Seed()).GetPaymentsAsync(Inst, 1, 50, null, null)).Data!.Results;

        Assert.Equal(9, rows.Count());
        Assert.Equal(Everything, rows.Sum(r => r.Amount));
        Assert.Equal(["Accra Chapter", "Class of 2020"], rows.Select(r => r.AccountName).Where(a => a is not null).Distinct().Order().ToArray());
        Assert.Equal(6, rows.Count(r => r.AccountName is null));   // everything else went to the institution's own account
    }

    [Fact]
    public async Task The_payout_forecast_keeps_the_institutions_own_payout_apart_from_what_goes_into_group_and_year_group_accounts()
    {
        var name = Seed(PayoutWindowCalculator.GetWindows(DateTime.UtcNow).NextWindowStart.AddMinutes(5));
        var ctx = TestDb.Create(name);
        var service = new PayoutService(
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx), new AlumniPgRepository<Community>(ctx),
            new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<ServiceType>(ctx), NullLogger<PayoutService>.Instance);

        var forecast = (await service.GetForecastAsync()).Data!;
        var inst = Assert.Single(forecast.Institutions);

        // Online payments only: the institution's own account gets the main fundraiser, the pending group's (still the institution's), a
        // ticket, the store order and the service request.
        Assert.Equal((290m, 5), (inst.NextPayout.Amount, inst.NextPayout.TransactionCount));
        // The year group's fundraiser, the group's fundraiser and the group event's ticket went elsewhere, and are still shown.
        Assert.Equal((250m, 3), (inst.NextOtherAccounts!.Amount, inst.NextOtherAccounts.TransactionCount));
        Assert.Equal(250m, forecast.Totals.NextOtherAccounts!.Amount);
    }
}

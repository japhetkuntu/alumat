using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>
/// One of each kind of payment, settled into the main account, a year group's own account and a group's own account, run through every
/// figure the institution portal shows. Wherever the money is paid into, it was collected by the institution, so the "collected"
/// figures must include all of it; only the payout figures divide it up by account.
/// </summary>
public class PaymentsAcrossAccountsTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Super = new() { Id = "admin", FirstName = "A", LastName = "B", Role = "SuperAdmin" };

    // Collected, by kind: dues/fundraisers 4 x 100 on Paystack plus 25 in cash, tickets 30 + 50, a store order 40, a service request 20.
    private const decimal Contributions = 425m;
    private const decimal Tickets = 80m;
    private const decimal Store = 40m;
    private const decimal Services = 20m;
    private const decimal Everything = Contributions + Tickets + Store + Services;

    private static readonly DateTime Now = DateTime.UtcNow;

    private static object[] Scenario()
    {
        var at = Now.AddDays(-1);
        return
        [
            new InstitutionEntity { Id = Tenant, Name = "UMaT", Slug = "umat", PaystackSubaccountCode = "ACCT_main" },
            new Batch { Id = "b2020", Name = "Class of 2020", Year = 2020, PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_year" },
            new Community { Id = "g1", Name = "Accra Chapter", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_group" },
            new Community { Id = "g2", Name = "Kumasi Chapter", PayoutStatus = "Pending" },
            new Campaign { Id = "cMain", Title = "Annual dues" },
            new Campaign { Id = "cYear", Title = "Class gift", YearGroups = [2020] },
            new Campaign { Id = "cGroup", Title = "Chapter hall", CommunityId = "g1" },
            new Campaign { Id = "cPending", Title = "Chapter outing", CommunityId = "g2" },
            new Contribution { CampaignId = "cMain", MemberId = "m1", Status = "Successful", PaymentMethod = "Paystack", Amount = 100, ConfirmedAt = at, CreatedAt = at },
            new Contribution { CampaignId = "cYear", MemberId = "m1", Status = "Successful", PaymentMethod = "Paystack", Amount = 100, ConfirmedAt = at, CreatedAt = at },
            new Contribution { CampaignId = "cGroup", MemberId = "m1", Status = "Successful", PaymentMethod = "Paystack", Amount = 100, ConfirmedAt = at, CreatedAt = at },
            new Contribution { CampaignId = "cPending", MemberId = "m1", Status = "Successful", PaymentMethod = "Paystack", Amount = 100, ConfirmedAt = at, CreatedAt = at },
            new Contribution { CampaignId = "cMain", MemberId = "m2", Status = "Successful", PaymentMethod = "Cash", Amount = 25, ConfirmedAt = at, CreatedAt = at },
            new AlumniEvent { Id = "eMain", Title = "Homecoming" },
            new AlumniEvent { Id = "eGroup", Title = "Chapter dinner", CommunityId = "g1" },
            new EventRsvp { EventId = "eMain", MemberId = "m1", Status = EventRsvpStatuses.Confirmed, AmountPaid = 30, PaidAt = at, CreatedAt = at },
            new EventRsvp { EventId = "eGroup", MemberId = "m1", Status = EventRsvpStatuses.Confirmed, AmountPaid = 50, PaidAt = at, CreatedAt = at },
            new StoreOrder { Id = "o1", Status = "Successful", PaymentMethod = "Paystack", TotalAmount = 40, ConfirmedAt = at, CreatedAt = at },
            new ServiceRequest { Id = "r1", ServiceTypeId = "s1", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 20, ConfirmedAt = at, CreatedAt = at },
        ];
    }

    private static AlumniDbContext Seeded(out string name)
    {
        name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in Scenario()) { if (e is ITenantScoped t && string.IsNullOrEmpty(t.InstitutionId)) t.InstitutionId = Tenant; db.Add(e); }
            db.SaveChanges();
        }
        return TestDb.Create(name, Tenant);
    }

    [Fact]
    public async Task The_analytics_money_figure_includes_every_kind_of_payment_whichever_account_it_settled_into()
    {
        using var ctx = Seeded(out _);
        var service = new AnalyticsService(
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Department>(ctx), new AlumniPgRepository<CommunityMembership>(ctx),
            new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx),
            new AlumniPgRepository<ServiceRequest>(ctx), new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<ForumThread>(ctx),
            new AlumniPgRepository<ForumPost>(ctx), TestDb.Tenant(Tenant), new InMemoryRedisService<InstitutionRedisConfig>(), NullLogger<AnalyticsService>.Instance);

        var money = (await service.GetAnalyticsAsync(Super, [])).Data!.Money!;

        Assert.Equal(Everything, money.ThisYear);
    }

    [Fact]
    public async Task The_report_summary_total_includes_every_kind_of_payment_whichever_account_it_settled_into()
    {
        using var ctx = Seeded(out _);
        var service = new ReportService(
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<Campaign>(ctx),
            new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<CommunityMembership>(ctx),
            new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx), new AlumniPgRepository<EventRsvp>(ctx), TestDb.Tenant(Tenant),
            new InMemoryRedisService<InstitutionRedisConfig>(), NullLogger<ReportService>.Instance);

        var summary = (await service.GetReportSummaryAsync(Super)).Data!;

        Assert.Equal(Everything, summary.TotalCollected);
    }
}

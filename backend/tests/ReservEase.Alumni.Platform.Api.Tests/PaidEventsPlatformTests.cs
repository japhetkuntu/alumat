using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>The platform decides who may charge for event tickets, and ticket money counts like every other payment.</summary>
public class PaidEventsPlatformTests
{
    private static (InstitutionManagementService s, string db) Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        var service = new InstitutionManagementService(ctx,
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<InstitutionStaff>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<AlumniEvent>(ctx),
            new AlumniPgRepository<PaymentTransaction>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<ForumCategory>(ctx), new AlumniPgRepository<PlatformStaff>(ctx), Mock.Of<IAuditLogService>(), Mock.Of<IPaystackService>(),
            new ConfigurationBuilder().Build(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
            MsOptions.Create(new MailtrapConfig()), Mock.Of<IRedisService<PlatformRedisConfig>>(), new FixedScope(), NullLogger<InstitutionManagementService>.Instance);
        return (service, name);
    }

    private static Institution Inst(string? subaccount = "ACCT_x", bool paidEvents = false) =>
        new() { Id = "i1", Name = "UMaT", Slug = "umat", Status = "Active", PaystackSubaccountCode = subaccount, PaidEventsEnabled = paidEvents };

    // ── The switch ──────────────────────────────────────────────────────

    [Fact]
    public async Task Paid_events_start_off_for_every_institution()
    {
        var (s, _) = Create(Inst());
        Assert.False((await s.GetInstitutionAsync("i1")).Data!.PaidEventsEnabled);
    }

    [Fact]
    public async Task Paid_events_can_be_turned_on_and_off()
    {
        var (s, db) = Create(Inst());

        var on = await s.UpdatePaidEventsAsync("i1", new UpdatePaidEventsRequest { Enabled = true }, "staff", "Staff");
        Assert.Equal(200, on.Code);
        Assert.True(on.Data!.PaidEventsEnabled);
        Assert.True((await TestDb.Create(db).Institutions.SingleAsync()).PaidEventsEnabled);

        var off = await s.UpdatePaidEventsAsync("i1", new UpdatePaidEventsRequest { Enabled = false }, "staff", "Staff");
        Assert.False(off.Data!.PaidEventsEnabled);
    }

    [Fact]
    public async Task Paid_events_cannot_be_turned_on_without_a_payout_account()
    {
        var (s, db) = Create(Inst(subaccount: null));

        var response = await s.UpdatePaidEventsAsync("i1", new UpdatePaidEventsRequest { Enabled = true }, "staff", "Staff");

        Assert.Equal(400, response.Code);
        Assert.Contains("payout account", response.Message);
        Assert.False((await TestDb.Create(db).Institutions.SingleAsync()).PaidEventsEnabled);
    }

    [Fact]
    public async Task Turning_paid_events_off_works_even_without_a_payout_account()
    {
        var (s, _) = Create(Inst(subaccount: null, paidEvents: true));
        Assert.Equal(200, (await s.UpdatePaidEventsAsync("i1", new UpdatePaidEventsRequest { Enabled = false }, "staff", "Staff")).Code);
    }

    [Fact]
    public async Task An_unknown_institution_is_404()
    {
        var (s, _) = Create(Inst());
        Assert.Equal(404, (await s.UpdatePaidEventsAsync("nope", new UpdatePaidEventsRequest { Enabled = true }, "staff", "Staff")).Code);
    }

    // ── Ticket money in the totals ──────────────────────────────────────

    private static EventRsvp Ticket(string id, string status, decimal paid = 100m, decimal fee = 3m, string reference = "ET_x") => new()
    {
        Id = id, InstitutionId = "i1", EventId = "e1", MemberId = "m1", Status = status, AmountPaid = paid, PlatformFeeAmount = fee,
        TransactionRef = reference + id, PaidAt = status == "Confirmed" ? DateTime.UtcNow : null,
    };

    [Fact]
    public async Task Paid_tickets_count_toward_what_the_institution_collected_and_what_the_platform_earned()
    {
        var (s, _) = Create(Inst(), new AlumniEvent { Id = "e1", InstitutionId = "i1", Title = "Gala", Venue = "Hall" },
            Ticket("t1", "Confirmed", 100m, 3m), Ticket("t2", "Confirmed", 100m, 3m),
            Ticket("t3", "PendingPayment"), Ticket("t4", "PaymentFailed"),
            new EventRsvp { Id = "free", InstitutionId = "i1", EventId = "e1", MemberId = "m9", Status = "Confirmed" });   // a free RSVP is not a payment

        var revenue = (await s.GetRevenueAsync("i1")).Data!;

        Assert.Equal(200m, revenue.GrossCollected);
        Assert.Equal(6m, revenue.PlatformFeeTotal);
        Assert.Equal(200m, revenue.NetToInstitution);   // nothing is deducted from the ticket price
        Assert.Equal(2, revenue.ConfirmedPaymentCount);
    }

    [Fact]
    public async Task The_payments_list_shows_tickets_and_still_adds_up_to_the_totals()
    {
        var (s, _) = Create(Inst(), new AlumniEvent { Id = "e1", InstitutionId = "i1", Title = "Gala", Venue = "Hall" },
            new MemberEntity { Id = "m1", InstitutionId = "i1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" },
            new StoreOrder { Id = "o1", InstitutionId = "i1", Status = "Successful", TotalAmount = 40, PlatformFeeAmount = 0.80m, ConfirmedAt = DateTime.UtcNow },
            Ticket("t1", "Confirmed", 100m, 3m), Ticket("t2", "PendingPayment"), Ticket("t3", "PaymentFailed"));

        var rows = (await s.GetPaymentsAsync("i1", 1, 50, null, null)).Data!.Results;
        var revenue = (await s.GetRevenueAsync("i1")).Data!;

        Assert.Equal(revenue.PlatformFeeTotal, rows.Sum(r => r.PlatformEarnedAmount));
        var tickets = rows.Where(r => r.Source == "EventTicket").ToList();
        Assert.Equal(3, tickets.Count);
        var paid = tickets.Single(t => t.Id == "t1");
        Assert.Equal(("Successful", "Gala", "Ama Mensah", 100m), (paid.Status, paid.Description, paid.PayerName, paid.Amount));
        Assert.Equal("Pending", tickets.Single(t => t.Id == "t2").Status);
        Assert.Equal("Failed", tickets.Single(t => t.Id == "t3").Status);
        Assert.Equal(0m, tickets.Single(t => t.Id == "t2").PlatformEarnedAmount);
    }

    [Fact]
    public async Task The_ticket_detail_reports_the_same_earned_figure_as_its_row()
    {
        var (s, _) = Create(Inst(), new AlumniEvent { Id = "e1", InstitutionId = "i1", Title = "Gala", Venue = "Hall" }, Ticket("t1", "Confirmed", 100m, 3m));

        var detail = (await s.GetPaymentDetailAsync("i1", "t1", "EventTicket")).Data!;

        Assert.Equal(("EventTicket", 100m, 3m, 3m, "Gala"), (detail.Source, detail.Amount, detail.PlatformFeeAmount, detail.PlatformEarnedAmount, detail.CampaignTitle));
    }

    [Fact]
    public async Task The_dashboard_total_includes_ticket_earnings()
    {
        var (s, _) = Create(Inst(), Ticket("t1", "Confirmed", 100m, 3m));
        Assert.Equal(3m, (await s.GetDashboardSummaryAsync()).Data!.Revenue);
    }
}

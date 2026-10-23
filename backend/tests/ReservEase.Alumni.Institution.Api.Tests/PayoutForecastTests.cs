using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>What the institution is told it will be paid: every kind of payment counts the same way, including paid event tickets.</summary>
public class PayoutForecastTests
{
    private const string Tenant = "inst-1";

    private static async Task<(decimal Amount, int Count)> NextPayoutAsync(Action<AlumniDbContextSeeder> seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            seed(new AlumniDbContextSeeder(db));
            await db.SaveChangesAsync();
        }
        var ctx = TestDb.Create(name, Tenant);
        var service = new PayoutService(
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<InstitutionEntity>(ctx), TestDb.Tenant(Tenant, "umat"), NullLogger<PayoutService>.Instance);
        var result = await service.GetForecastAsync();
        var next = result.Data!.NextPayout;
        return (next.Amount, next.TransactionCount);
    }

    private sealed class AlumniDbContextSeeder(PostgresDb.Sdk.DbContexts.AlumniDbContext db)
    {
        public void Add(object e) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
    }

    private static DateTime InNextWindow() => PayoutWindowCalculator.GetWindows(DateTime.UtcNow).NextWindowStart.AddMinutes(5);

    [Fact]
    public async Task A_paid_ticket_counts_for_the_price_the_institution_receives()
    {
        var (amount, count) = await NextPayoutAsync(s => s.Add(new EventRsvp
        {
            EventId = "e1", MemberId = "m1", Status = EventRsvpStatuses.Confirmed, AmountPaid = 150m, GrossChargeAmount = 157m, PaidAt = InNextWindow(),
        }));
        Assert.Equal(150m, amount);   // not the 157 the payer was charged
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Unpaid_tickets_do_not_count_and_tickets_add_to_the_other_payments()
    {
        var (amount, count) = await NextPayoutAsync(s =>
        {
            s.Add(new EventRsvp { EventId = "e1", MemberId = "m1", Status = EventRsvpStatuses.PendingPayment, AmountPaid = 150m });
            s.Add(new EventRsvp { EventId = "e1", MemberId = "m2", Status = EventRsvpStatuses.PaymentFailed, AmountPaid = 150m });
            s.Add(new EventRsvp { EventId = "e1", MemberId = "m3", Status = EventRsvpStatuses.Confirmed, AmountPaid = 0m, PaidAt = InNextWindow() });   // a free RSVP
            s.Add(new Contribution { CampaignId = "c1", MemberId = "m4", Status = "Successful", PaymentMethod = "Paystack", Amount = 50m, ConfirmedAt = InNextWindow() });
            s.Add(new EventRsvp { EventId = "e1", MemberId = "m5", Status = EventRsvpStatuses.Confirmed, AmountPaid = 40m, PaidAt = InNextWindow() });
        });
        Assert.Equal(90m, amount);
        Assert.Equal(2, count);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>The day-by-day record of what was paid out: each payment lands on the working day it is paid out, like the forecast says.</summary>
public class PayoutHistoryTests
{
    private const string Tenant = "inst-1";

    private static PayoutService Create(Action<AlumniDbContext> seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant)) { seed(db); db.SaveChanges(); }
        var ctx = TestDb.Create(name, Tenant);
        return new PayoutService(
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<InstitutionEntity>(ctx), TestDb.Tenant(Tenant, "umat"), NullLogger<PayoutService>.Instance, new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<ServiceType>(ctx));
    }

    private static Contribution Paid(string campaignId, decimal amount, DateTime at, string member = "m1") => new()
    {
        CampaignId = campaignId, MemberId = member, Status = "Successful", PaymentMethod = "Paystack", Amount = amount, ConfirmedAt = at,
        Member = new MemberSnapshot { FirstName = "Ama", LastName = "Mensah" },
    };

    private static DateTime At(DateOnly day, int hour = 10) => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    // A fixed past working week, so the weekday maths in these tests does not depend on today's date.
    private static DateOnly Monday => PayoutWindowCalculator.PreviousWorkingDayOnOrBefore(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-14)) is var d
        ? d.AddDays(-(((int)d.DayOfWeek + 6) % 7)) : default;

    [Fact]
    public async Task A_payment_is_paid_out_on_the_next_working_day_and_a_weekend_waits_for_monday()
    {
        var mon = Monday; var tue = mon.AddDays(1); var fri = mon.AddDays(4); var nextMon = mon.AddDays(7);
        var service = Create(db =>
        {
            db.Add(new Campaign { Id = "dues", Title = "2026 Dues", IsMembershipCampaign = true, InstitutionId = Tenant });
            db.Add(Paid("dues", 100m, At(mon)));                 // Monday  -> Tuesday
            db.Add(Paid("dues", 50m, At(fri)));                  // Friday  -> next Monday
            db.Add(Paid("dues", 25m, At(fri.AddDays(1))));       // Saturday -> next Monday
            db.Add(Paid("dues", 10m, At(fri.AddDays(2))));       // Sunday  -> next Monday
        });

        var history = (await service.GetHistoryAsync(nextMon.AddDays(1), 30, null)).Data!;
        var byDate = history.Days.ToDictionary(d => d.Date);
        Assert.Equal(100m, byDate[tue].Amount);
        Assert.Equal(85m, byDate[nextMon].Amount);               // Friday + Saturday + Sunday
        Assert.Equal(3, byDate[nextMon].TransactionCount);
        Assert.Equal(2, history.Days.Count);
        Assert.Equal(185m, history.TotalAmount);
        Assert.True(history.Days[0].Date > history.Days[1].Date);   // newest first
    }

    [Fact]
    public async Task Each_day_says_what_kind_of_payment_it_was_made_of()
    {
        var mon = Monday; var tue = mon.AddDays(1);
        var service = Create(db =>
        {
            db.Add(new Campaign { Id = "dues", Title = "Dues", IsMembershipCampaign = true, InstitutionId = Tenant });
            db.Add(new Campaign { Id = "fund", Title = "Library", IsMembershipCampaign = false, InstitutionId = Tenant });
            db.Add(Paid("dues", 200m, At(mon)));
            db.Add(Paid("fund", 40m, At(mon, 14)));
            db.Add(new StoreOrder { OrderNumber = "ORD-1", MemberId = "m2", Status = "Successful", PaymentMethod = "Paystack", TotalAmount = 60m, ConfirmedAt = At(mon, 9) });
            db.Add(new ServiceRequest { RequestNumber = "R1", MemberId = "m3", ServiceTypeName = "Transcript", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 30m, ConfirmedAt = At(mon, 11) });
            db.Add(new EventRsvp { EventId = "e1", MemberId = "m4", Status = EventRsvpStatuses.Confirmed, AmountPaid = 20m, PaidAt = At(mon, 16) });
        });

        var day = (await service.GetHistoryAsync(tue, 7, null)).Data!.Days.Single(d => d.Date == tue);
        Assert.Equal(350m, day.Amount);
        Assert.Equal(5, day.TransactionCount);
        Assert.Equal(new[] { "Dues", "Store", "Fundraisers", "Services", "Event RSVPs" }, day.Sources.Select(s => s.Source));   // biggest first
    }

    [Fact]
    public async Task A_year_group_with_its_own_account_is_paid_out_separately_and_can_be_viewed_alone()
    {
        var mon = Monday; var tue = mon.AddDays(1);
        var service = Create(db =>
        {
            db.Add(new Campaign { Id = "c1", Title = "Class of 2019", YearGroups = [2019], InstitutionId = Tenant });
            db.Add(new Campaign { Id = "c3", Title = "Class of 2020 on main", YearGroups = [2020], InstitutionId = Tenant });
            db.Add(new Batch { Id = "b19", Name = "Batch of 2019", Year = 2019, PayoutStatus = "Approved", UseInstitutionAccount = false, PaystackSubaccountCode = "ACCT_x", InstitutionId = Tenant });
            db.Add(new Batch { Id = "b20", Name = "Batch of 2020", Year = 2020, PayoutStatus = "Approved", UseInstitutionAccount = true, InstitutionId = Tenant });
            db.Add(Paid("c1", 500m, At(mon)));                                                                       // the 2019 group's own account
            db.Add(Paid("c3", 40m, At(mon)));                                                                        // a group that uses the main account
            db.Add(new Contribution { CampaignId = "c2", MemberId = "m", Status = "Pending", PaymentMethod = "Paystack", Amount = 70m, ConfirmedAt = At(mon) });
            db.Add(new Contribution { CampaignId = "c2", MemberId = "m", Status = "Successful", PaymentMethod = "BankTransfer", Amount = 80m, ConfirmedAt = At(mon) });
            db.Add(Paid("c2", 15m, At(mon)));
        });

        var all = (await service.GetHistoryAsync(tue, 7, null)).Data!;
        Assert.Equal(555m, all.TotalAmount);                                                    // pending and non-Paystack are left out
        Assert.Equal(["main", "b19"], all.AccountOptions.Select(a => a.Key));                  // a group on the main account is not its own account
        var day = Assert.Single(all.Days);
        Assert.Equal(55m, day.Accounts.Single(a => a.Key == "main").Amount);
        Assert.Equal(500m, day.Accounts.Single(a => a.Key == "b19").Amount);
        Assert.Equal("main", day.Accounts[0].Key);                                              // the main account is listed first

        var group = (await service.GetHistoryAsync(tue, 7, "b19")).Data!;
        Assert.Equal(500m, group.TotalAmount);
        Assert.Equal("b19", group.Account);
        Assert.Equal(55m, (await service.GetHistoryAsync(tue, 7, "main")).Data!.TotalAmount);
        Assert.Equal(555m, (await service.GetHistoryAsync(tue, 7, "nope")).Data!.TotalAmount);  // an unknown account means all

        var detail = (await service.GetDayAsync(tue, "b19")).Data!;
        Assert.Equal(500m, detail.Amount);
        Assert.Equal("Batch of 2019", Assert.Single(detail.Items).Account);
    }

    [Fact]
    public async Task A_group_with_its_own_account_is_paid_out_separately_for_its_fundraisers_and_its_events()
    {
        var mon = Monday; var tue = mon.AddDays(1);
        var service = Create(db =>
        {
            db.Add(new Community { Id = "g1", Name = "Accra Chapter", PayoutStatus = "Approved", UseInstitutionAccount = false, PaystackSubaccountCode = "ACCT_g", InstitutionId = Tenant });
            db.Add(new Community { Id = "g2", Name = "Quiet Group", PayoutStatus = "Pending", InstitutionId = Tenant });
            db.Add(new Campaign { Id = "gc", Title = "Chapter hall", CommunityId = "g1", InstitutionId = Tenant });
            db.Add(new Campaign { Id = "pc", Title = "Pending group fund", CommunityId = "g2", InstitutionId = Tenant });
            db.Add(new Campaign { Id = "mc", Title = "General", InstitutionId = Tenant });
            db.Add(new AlumniEvent { Id = "ge", Title = "Chapter dinner", CommunityId = "g1", InstitutionId = Tenant });
            db.Add(new AlumniEvent { Id = "me", Title = "Homecoming", InstitutionId = Tenant });
            db.Add(Paid("gc", 300m, At(mon)));       // the group's account
            db.Add(Paid("pc", 20m, At(mon)));        // group not approved yet: main
            db.Add(Paid("mc", 10m, At(mon)));        // main
            db.Add(new EventRsvp { EventId = "ge", MemberId = "m4", Status = EventRsvpStatuses.Confirmed, AmountPaid = 50m, PaidAt = At(mon, 12) });   // group's account
            db.Add(new EventRsvp { EventId = "me", MemberId = "m5", Status = EventRsvpStatuses.Confirmed, AmountPaid = 5m, PaidAt = At(mon, 12) });    // main
        });

        var all = (await service.GetHistoryAsync(tue, 7, null)).Data!;
        Assert.Equal(385m, all.TotalAmount);
        Assert.Equal(["main", "group:g1"], all.AccountOptions.Select(a => a.Key));
        var day = Assert.Single(all.Days);
        Assert.Equal(35m, day.Accounts.Single(a => a.Key == "main").Amount);
        Assert.Equal(350m, day.Accounts.Single(a => a.Key == "group:g1").Amount);

        Assert.Equal(350m, (await service.GetHistoryAsync(tue, 7, "group:g1")).Data!.TotalAmount);
        Assert.Equal(35m, (await service.GetHistoryAsync(tue, 7, "main")).Data!.TotalAmount);

        var detail = (await service.GetDayAsync(tue, "group:g1")).Data!;
        Assert.Equal(350m, detail.Amount);
        Assert.Equal(2, detail.Items.Count);
        Assert.All(detail.Items, i => Assert.Equal("Accra Chapter", i.Account));
        Assert.Equal(35m, (await service.GetDayAsync(tue, "main")).Data!.Amount);
    }

    [Fact]
    public async Task A_service_is_paid_into_the_account_it_names_or_its_groups_by_default()
    {
        var mon = Monday; var tue = mon.AddDays(1);
        var service = Create(db =>
        {
            db.Add(new Community { Id = "g1", Name = "Accra Chapter", PayoutStatus = "Approved", UseInstitutionAccount = false, PaystackSubaccountCode = "ACCT_g", InstitutionId = Tenant });
            db.Add(new Batch { Id = "b19", Name = "Batch of 2019", Year = 2019, PayoutStatus = "Approved", UseInstitutionAccount = false, PaystackSubaccountCode = "ACCT_b", InstitutionId = Tenant });
            db.Add(new ServiceType { Id = "plain", Name = "Transcript", InstitutionId = Tenant });
            db.Add(new ServiceType { Id = "ofgroup", Name = "Chapter letter", CommunityId = "g1", InstitutionId = Tenant });       // for a group: its account by default
            db.Add(new ServiceType { Id = "forced", Name = "Attestation", CommunityId = "g1", SettlementTarget = "institution", InstitutionId = Tenant });
            db.Add(new ServiceType { Id = "toyear", Name = "Class letter", SettlementTarget = "year:2019", InstitutionId = Tenant });
            db.Add(new ServiceRequest { RequestNumber = "1", MemberId = "m", ServiceTypeId = "plain", ServiceTypeName = "Transcript", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 10m, ConfirmedAt = At(mon) });
            db.Add(new ServiceRequest { RequestNumber = "2", MemberId = "m", ServiceTypeId = "ofgroup", ServiceTypeName = "Chapter letter", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 20m, ConfirmedAt = At(mon) });
            db.Add(new ServiceRequest { RequestNumber = "3", MemberId = "m", ServiceTypeId = "forced", ServiceTypeName = "Attestation", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 40m, ConfirmedAt = At(mon) });
            db.Add(new ServiceRequest { RequestNumber = "4", MemberId = "m", ServiceTypeId = "toyear", ServiceTypeName = "Class letter", PaymentStatus = "Successful", PaymentMethod = "Paystack", Amount = 80m, ConfirmedAt = At(mon) });
        });

        var all = (await service.GetHistoryAsync(tue, 7, null)).Data!;
        var day = Assert.Single(all.Days);
        Assert.Equal(50m, day.Accounts.Single(a => a.Key == "main").Amount);            // plain + forced to the institution
        Assert.Equal(20m, day.Accounts.Single(a => a.Key == "group:g1").Amount);
        Assert.Equal(80m, day.Accounts.Single(a => a.Key == "b19").Amount);

        var detail = (await service.GetDayAsync(tue, "group:g1")).Data!;
        Assert.Equal(20m, detail.Amount);
        Assert.Equal("Accra Chapter", Assert.Single(detail.Items).Account);
        Assert.Equal(50m, (await service.GetDayAsync(tue, "main")).Data!.Amount);
    }

    [Fact]
    public async Task A_days_detail_lists_each_payment_with_who_paid_and_for_what()
    {
        var mon = Monday; var tue = mon.AddDays(1);
        var service = Create(db =>
        {
            db.Add(new Campaign { Id = "dues", Title = "2026 Dues", IsMembershipCampaign = true, InstitutionId = Tenant });
            db.Add(new AlumniEvent { Id = "e1", Title = "Homecoming", InstitutionId = Tenant });
            db.Add(Paid("dues", 100m, At(mon, 9)));
            db.Add(new ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member { Id = "m4", FirstName = "Kofi", LastName = "Boateng", Email = "k@x.org", InstitutionId = Tenant });
            db.Add(new EventRsvp { EventId = "e1", MemberId = "m4", Status = EventRsvpStatuses.Confirmed, AmountPaid = 20m, PaidAt = At(mon, 12) });
        });

        var detail = (await service.GetDayAsync(tue, null)).Data!;
        Assert.Equal(120m, detail.Amount);
        Assert.Equal(2, detail.Items.Count);
        Assert.Equal("Homecoming", detail.Items[0].Title);            // latest first
        Assert.Equal("Kofi Boateng", detail.Items[0].PayerName);
        Assert.Equal("2026 Dues", detail.Items[1].Title);
        Assert.Equal("Ama Mensah", detail.Items[1].PayerName);
    }

    [Fact]
    public async Task A_weekend_or_future_day_has_nothing_and_the_history_never_includes_upcoming_payouts()
    {
        var sat = Monday.AddDays(5);
        var service = Create(db => db.Add(Paid("c", 10m, DateTime.UtcNow.AddMinutes(-5))));   // paid today: not out until the next working day
        Assert.Empty((await service.GetDayAsync(sat, null)).Data!.Items);
        var history = (await service.GetHistoryAsync(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), 30, null)).Data!;
        Assert.DoesNotContain(history.Days, d => d.Date > PayoutWindowCalculator.GetWindows(DateTime.UtcNow).LastPayoutDate);
        Assert.Equal(0m, history.TotalAmount);
    }

    [Fact]
    public async Task A_busy_day_lists_only_the_newest_payments_but_its_total_and_count_cover_them_all()
    {
        var mon = Monday; var tue = mon.AddDays(1);
        var service = Create(db =>
        {
            db.Add(new Campaign { Id = "dues", Title = "2026 Dues", IsMembershipCampaign = true, InstitutionId = Tenant });
            for (var i = 0; i < PayoutService.DayItemLimit + 20; i++)
                db.Add(Paid("dues", 10m, At(mon, 9).AddMinutes(i)));
            db.Add(new StoreOrder { OrderNumber = "A1", TotalAmount = 40m, Status = "Successful", PaymentMethod = "Paystack", ConfirmedAt = At(mon, 20) });
        });

        var detail = (await service.GetDayAsync(tue, null)).Data!;

        Assert.Equal(PayoutService.DayItemLimit, detail.Items.Count);
        Assert.Equal(PayoutService.DayItemLimit + 21, detail.TotalCount);
        Assert.Equal((PayoutService.DayItemLimit + 20) * 10m + 40m, detail.Amount);
        Assert.Equal("Order A1", detail.Items[0].Title);                                  // newest first
        var storeOnly = (await service.GetDayAsync(tue, "nope")).Data!;                   // an unknown account means all
        Assert.Equal(detail.TotalCount, storeOnly.TotalCount);
    }
}

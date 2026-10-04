using Microsoft.EntityFrameworkCore;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.Contributions;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class ContributionCallbackWorkflowTests(TemporalFixture temporal)
{
    private const string Inst = "i1";
    private static readonly int ThisYear = DateTime.UtcNow.Year;

    private sealed class Rig : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
        public Mock<IPaystackService> Paystack { get; } = new();
        public InMemoryRedisService<MemberRedisConfig> Redis { get; } = new();
        public CapturingLogger<ContributionCallbackActivities> Log { get; } = new();
        public ContributionCallbackActivities Activities { get; }

        public Rig()
        {
            var (first, conn) = TestDb.CreateRelational();
            first.Dispose();
            connection = conn;
            var ctx = TestDb.OpenRelational(connection);
            var provider = new Mock<ITemporalClientProvider>();
            provider.SetupGet(p => p.IsAvailable).Returns(false);   // notification enqueue is then logged as dropped, which tests can see
            Activities = new ContributionCallbackActivities(
                new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
                new AlumniPgRepository<PaymentTransaction>(ctx), new AlumniPgRepository<RecurringContribution>(ctx),
                new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution>(ctx), new AlumniPgRepository<Referral>(ctx),
                Paystack.Object, Redis, provider.Object, Log);
        }

        public AlumniDbContext Db() => TestDb.OpenRelational(connection);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) db.Add(e);
            await db.SaveChangesAsync();
        }

        public void Verify(bool ok, string status, long amountSubunit = 0, long? fees = null, string message = "ok", PaystackAuthorization? auth = null) =>
            Paystack.Setup(p => p.VerifyPaymentAsync(It.IsAny<string>())).ReturnsAsync(new VerifyPaymentResponse
            {
                Status = ok, Message = message,
                Data = new VerifyPaymentData { Status = status, Amount = amountSubunit, Fees = fees, GatewayResponse = "Approved", Authorization = auth },
            });

        public bool NotificationWasDispatched => Log.Entries.Any(e => e.Message.Contains("ContributionConfirmed"));
        public void Dispose() => connection.Dispose();
    }

    private Task<PaymentCallbackResult> Run(Rig rig, string reference = "CN_1", string? body = null) =>
        WorkflowHarness.RunCallback<ProcessContributionCallbackWorkflow>(temporal.Client, reference, body, rig.Activities);

    private static PaymentTransaction Tx(string status = "Pending", string memberId = "m1", decimal amount = 100) => new()
    {
        Id = "t1", Reference = "CN_1", MemberId = memberId, CampaignId = "c1", Status = status, Amount = amount, InstitutionId = Inst,
        PlatformFeeAmount = 2, GatewayFeeAmount = 1.9m, TransactionChargeAmount = 4, GrossChargeAmount = 103.9m,
        Member = memberId == "" ? null : new MemberSnapshot { Id = memberId, FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" },
    };

    private static Campaign Camp(bool membership = false, int? year = null) => new()
    {
        Id = "c1", Title = "Library fund", InstitutionId = Inst, IsMembershipCampaign = membership, MembershipYear = year, Deadline = DateTime.UtcNow.AddDays(10),
    };

    private static MemberEntity Person(string status = "Active", int gradYear = 2015, string? number = null) => new()
    {
        Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com", Status = status, GraduationYear = gradYear, MemberNumber = number, InstitutionId = Inst,
    };

    private static ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution Institution(string policy = "DuesRequired") =>
        new() { Id = Inst, Slug = "umat", Name = "UMaT", MemberActivePolicy = policy };

    // ── Guards ──────────────────────────────────────────────────────────

    [WorkflowFact]
    public async Task An_unknown_reference_is_rejected()
    {
        using var rig = new Rig();
        var result = await Run(rig, "CN_none");
        Assert.True(result.IsBadRequest);
        Assert.Equal("Unknown payment reference.", result.Message);
    }

    [WorkflowFact]
    public async Task A_confirmed_transaction_is_not_verified_again_or_double_counted()
    {
        using var rig = new Rig();
        await rig.Seed(Tx("Successful"), Camp());

        var result = await Run(rig, body: "{\"event\":\"charge.success\"}");

        Assert.Contains("already verified", result.Message);
        rig.Paystack.Verify(p => p.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
        using var db = rig.Db();
        Assert.Equal("{\"event\":\"charge.success\"}", (await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync()).CallbackPayload);
        Assert.Equal(0, (await db.Campaigns.IgnoreQueryFilters().SingleAsync()).PaidCount);
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowTheory]
    [InlineData("Invalid reference provided", "We couldn't find that payment reference. Please try again.")]
    [InlineData("Transaction could not find", "We couldn't find that payment reference. Please try again.")]
    [InlineData("Transaction already verified", "This payment has already been processed.")]
    [InlineData("Insufficient Funds", "Your card was declined. Please check with your bank or try another payment method.")]
    [InlineData("Card declined by issuer", "Your card was declined. Please check with your bank or try another payment method.")]
    [InlineData("The card has expired", "Your payment method has expired. Please use a different card.")]
    [InlineData("Not authorised", "The payment was not authorized. Please try again or use another payment method.")]
    [InlineData("Something odd happened", "We couldn't confirm your payment. Please try again or contact support.")]
    [InlineData("", "We couldn't confirm your payment. Please try again or contact support.")]
    public async Task A_failed_verification_fails_the_transaction_with_a_friendly_message(string raw, string friendly)
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        rig.Verify(false, "unknown", message: raw);

        var result = await Run(rig);

        Assert.True(result.IsBadRequest);
        Assert.Equal(friendly, result.Message);
        using var db = rig.Db();
        var tx = await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Failed", friendly), (tx.Status, tx.FailureMessage));
        Assert.NotNull(tx.ProcessedAt);
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    // ── Non-success Paystack states ─────────────────────────────────────

    [WorkflowFact]
    public async Task A_pending_paystack_status_leaves_the_transaction_pending_and_keeps_the_cache()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        await rig.Redis.SetAsync("paystack:ref:CN_1", new { MemberId = "m1" });
        rig.Verify(true, "pending", 10000);

        var result = await Run(rig);

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal("Pending", (await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync()).Status);
        Assert.Contains("paystack:ref:CN_1", rig.Redis.Store.Keys);
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowTheory]
    [InlineData("failed")]
    [InlineData("abandoned")]
    [InlineData("reversed")]
    public async Task A_failed_paystack_status_fails_the_transaction_and_clears_the_cache(string status)
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        await rig.Redis.SetAsync("paystack:ref:CN_1", new { MemberId = "m1" });
        rig.Verify(true, status, 10000);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal("Failed", (await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync()).Status);
        Assert.DoesNotContain("paystack:ref:CN_1", rig.Redis.Store.Keys);
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
        Assert.False(rig.NotificationWasDispatched);
    }

    // ── Successful one-off contribution ─────────────────────────────────

    [WorkflowFact]
    public async Task A_successful_payment_records_the_contribution_at_the_institutions_full_amount_and_confirms_the_member()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(amount: 100), Camp());
        await rig.Redis.SetAsync("paystack:ref:CN_1", new { MemberId = "m1" });
        rig.Verify(true, "success", amountSubunit: 10390, fees: 190);

        var result = await Run(rig);

        Assert.False(result.IsBadRequest);
        Assert.Equal("Payment verified and contribution recorded", result.Message);
        using var db = rig.Db();
        var tx = await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Successful", 100m, 103.90m, 1.90m), (tx.Status, tx.Amount, tx.GrossChargeAmount, tx.GatewayFeeAmount));
        Assert.Equal("Approved", tx.GatewayResponse);

        var c = await db.Contributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((100m, 100m, "Successful", "Paystack", "CN_1"), (c.Amount, c.NetAmountToInstitution, c.Status, c.PaymentMethod, c.TransactionRef));
        Assert.Equal(("m1", "c1", Inst, "Library fund"), (c.MemberId, c.CampaignId, c.InstitutionId, c.Campaign!.Title));
        Assert.Equal(4m - 1.9m, c.PlatformRevenueAmount);   // transaction charge minus Paystack's real fee
        Assert.Equal((2m, 1.9m, 103.9m), (c.PlatformFeeAmount, c.GatewayFeeAmount, c.GrossChargeAmount));
        Assert.Equal("Paystack", c.ConfirmedBy);

        var campaign = await db.Campaigns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((100m, 1), (campaign.CollectedAmount, campaign.PaidCount));
        Assert.DoesNotContain("paystack:ref:CN_1", rig.Redis.Store.Keys);
        Assert.True(rig.NotificationWasDispatched);
    }

    [WorkflowFact]
    public async Task An_unsplit_payment_with_no_subaccount_records_zero_platform_revenue_not_a_negative_one()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.PlatformFeeAmount = 0; tx.TransactionChargeAmount = 0; tx.GatewayFeeAmount = 0; tx.GrossChargeAmount = 100;
        await rig.Seed(tx, Camp());
        rig.Verify(true, "success", amountSubunit: 10000, fees: 190);   // Paystack's real fee is 1.90

        await Run(rig);

        using var db = rig.Db();
        var c = await db.Contributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((0m, 1.9m), (c.PlatformRevenueAmount, c.GatewayFeeAmount));
    }

    [WorkflowFact]
    public async Task The_institutions_amount_is_never_overwritten_by_paystacks_grossed_up_figure()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(amount: 100), Camp());
        rig.Verify(true, "success", amountSubunit: 999999);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(100m, (await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync()).Amount);
        Assert.Equal(100m, (await db.Contributions.IgnoreQueryFilters().SingleAsync()).Amount);
    }

    [WorkflowFact]
    public async Task When_paystack_reports_no_fee_the_estimated_gateway_fee_is_kept()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        rig.Verify(true, "success", 10390, fees: null);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(1.9m, (await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync()).GatewayFeeAmount);
    }

    [WorkflowFact]
    public async Task A_reconciliation_mismatch_still_records_the_payment()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        rig.Verify(true, "success", 50000, fees: 190);   // wildly different gross

        var result = await Run(rig);

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal(1, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task The_currency_and_channel_come_from_the_webhook_body()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        rig.Verify(true, "success", 10390);

        await Run(rig, body: "{\"data\":{\"currency\":\"GHS\",\"authorization\":{\"channel\":\"mobile_money\"}}}");

        using var db = rig.Db();
        var tx = await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("GHS", "mobile_money"), (tx.Currency, tx.Channel));
    }

    [WorkflowFact]
    public async Task A_garbled_webhook_body_does_not_stop_the_payment_being_recorded()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        rig.Verify(true, "success", 10390);

        var result = await Run(rig, body: "not json at all");

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal(1, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task A_contribution_already_recorded_for_the_reference_is_not_duplicated_or_recounted()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp(), new Contribution { Id = "existing", TransactionRef = "CN_1", MemberId = "m1", CampaignId = "c1", Amount = 100, Status = "Successful", InstitutionId = Inst });
        rig.Verify(true, "success", 10390);

        var result = await Run(rig);

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal(1, await db.Contributions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, (await db.Campaigns.IgnoreQueryFilters().SingleAsync()).PaidCount);
        Assert.Equal("Successful", (await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync()).Status);
        Assert.False(rig.NotificationWasDispatched);
    }

    [WorkflowFact]
    public async Task A_second_successful_contribution_adds_to_the_campaign_totals()
    {
        using var rig = new Rig();
        var campaign = Camp(); campaign.CollectedAmount = 250; campaign.PaidCount = 3;
        await rig.Seed(Tx(amount: 40), campaign);
        rig.Verify(true, "success", 4390);

        await Run(rig);

        using var db = rig.Db();
        var saved = await db.Campaigns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((290m, 4), (saved.CollectedAmount, saved.PaidCount));
    }

    [WorkflowFact]
    public async Task A_guest_payment_is_recorded_without_member_side_effects()
    {
        using var rig = new Rig();
        var tx = Tx(memberId: ""); tx.IsGuestPayment = true; tx.ShowOnWallOfSupport = true;
        await rig.Seed(tx, Camp());
        rig.Verify(true, "success", 10390);

        var result = await Run(rig);

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        var c = await db.Contributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((true, true, string.Empty), (c.IsGuestPayment, c.ShowOnWallOfSupport, c.MemberId));
        Assert.Null(c.Member);
    }

    [WorkflowFact]
    public async Task A_missing_member_snapshot_is_rebuilt_from_the_member_record()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.Member = null;
        await rig.Seed(tx, Camp(), Person());
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var c = await db.Contributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Ama", "ama@x.com"), (c.Member!.FirstName, c.Member.Email));
    }

    [WorkflowFact]
    public async Task The_contribution_belongs_to_the_campaigns_institution_not_an_ambient_tenant()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.InstitutionId = "";
        await rig.Seed(tx, Camp());
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(Inst, (await db.Contributions.IgnoreQueryFilters().SingleAsync()).InstitutionId);
    }

    // ── Recurring giving ────────────────────────────────────────────────

    private static PaystackAuthorization Card(bool reusable = true, string code = "AUTH_1") =>
        new() { AuthorizationCode = code, Reusable = reusable, Channel = "card", Last4 = "4081", CardType = "visa", Bank = "TEST BANK" };

    [WorkflowFact]
    public async Task A_monthly_gift_is_set_up_from_a_reusable_card_with_the_next_charge_a_month_away()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.SetupRecurringGiving = true;
        await rig.Seed(tx, Camp());
        rig.Verify(true, "success", 10390, auth: Card());

        await Run(rig);

        using var db = rig.Db();
        var r = await db.RecurringContributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("m1", "c1", 100m, "Active", "AUTH_1", "4081"), (r.MemberId, r.CampaignId, r.Amount, r.Status, r.AuthorizationCode, r.CardLast4));
        var days = (r.NextChargeDate - DateTime.UtcNow).TotalDays;
        Assert.InRange(days, 27, 32);
        Assert.Equal("Successful", r.LastChargeStatus);
    }

    [WorkflowFact]
    public async Task A_non_reusable_channel_skips_recurring_setup_but_the_contribution_still_succeeds()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.SetupRecurringGiving = true;
        await rig.Seed(tx, Camp());
        rig.Verify(true, "success", 10390, auth: Card(reusable: false));

        var result = await Run(rig);

        Assert.False(result.IsBadRequest);
        using var db = rig.Db();
        Assert.Equal(0, await db.RecurringContributions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task No_authorization_at_all_also_skips_recurring_setup()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.SetupRecurringGiving = true;
        await rig.Seed(tx, Camp());
        rig.Verify(true, "success", 10390, auth: null);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(0, await db.RecurringContributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task An_existing_active_monthly_gift_to_the_same_campaign_is_not_duplicated()
    {
        using var rig = new Rig();
        var tx = Tx(); tx.SetupRecurringGiving = true;
        await rig.Seed(tx, Camp(), new RecurringContribution { Id = "old", MemberId = "m1", CampaignId = "c1", Status = "Active", InstitutionId = Inst, AuthorizationCode = "A" });
        rig.Verify(true, "success", 10390, auth: Card());

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(1, await db.RecurringContributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task A_one_off_payment_never_creates_a_monthly_gift()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp());
        rig.Verify(true, "success", 10390, auth: Card());

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(0, await db.RecurringContributions.IgnoreQueryFilters().CountAsync());
    }

    // ── Membership dues ─────────────────────────────────────────────────

    [WorkflowFact]
    public async Task A_second_successful_charge_for_an_already_paid_membership_is_marked_duplicate_and_not_recorded()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp(true, ThisYear), Person(), Institution(),
            new Contribution { Id = "earlier", MemberId = "m1", CampaignId = "c1", Status = "Successful", Amount = 100, InstitutionId = Inst });
        await rig.Redis.SetAsync("paystack:ref:CN_1", new { MemberId = "m1" });
        rig.Verify(true, "success", 10390);

        var result = await Run(rig);

        Assert.True(result.IsBadRequest);
        Assert.Contains("already been paid", result.Message);
        using var db = rig.Db();
        var tx = await db.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Duplicate", tx.Status);
        Assert.Contains("refunded", tx.FailureMessage);
        Assert.Equal(1, await db.Contributions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, (await db.Campaigns.IgnoreQueryFilters().SingleAsync()).PaidCount);
        Assert.DoesNotContain("paystack:ref:CN_1", rig.Redis.Store.Keys);
        Assert.False(rig.NotificationWasDispatched);
    }

    [WorkflowFact]
    public async Task Paying_the_last_unpaid_year_makes_the_member_active_until_year_end_and_counts_the_years_paid()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp(true, ThisYear), Person(gradYear: ThisYear), Institution("DuesRequired"));
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var m = await db.Members.IgnoreQueryFilters().SingleAsync();
        Assert.True(m.IsMembershipActive);
        Assert.Equal(new DateTime(ThisYear, 12, 31, 23, 59, 59, DateTimeKind.Utc), m.MembershipExpiry);
        Assert.Equal(1, m.MembershipYearsPaid);
        Assert.NotNull(m.LastMembershipPaidAt);
    }

    [WorkflowFact]
    public async Task With_arrears_still_owing_the_member_is_not_active_and_has_no_expiry()
    {
        using var rig = new Rig();
        var older = Camp(true, ThisYear - 1); older.Id = "c-old";
        await rig.Seed(Tx(), Camp(true, ThisYear), older, Person(gradYear: ThisYear - 1), Institution("DuesRequired"));
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var m = await db.Members.IgnoreQueryFilters().SingleAsync();
        Assert.False(m.IsMembershipActive);
        Assert.Null(m.MembershipExpiry);
        Assert.Equal(1, m.MembershipYearsPaid);
    }

    [WorkflowFact]
    public async Task A_pending_member_who_pays_dues_is_auto_approved_with_the_next_member_number_for_their_year()
    {
        using var rig = new Rig();
        var existing = Person("Active", 2015, "UMAT-2015-0007"); existing.Id = "other"; existing.Email = "o@x.com";
        await rig.Seed(Tx(), Camp(true, ThisYear), Person("Pending", 2015), existing, Institution());
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var m = await db.Members.IgnoreQueryFilters().SingleAsync(x => x.Id == "m1");
        Assert.Equal(("Active", "UMAT-2015-0008", "system"), (m.Status, m.MemberNumber, m.UpdatedBy));
    }

    [WorkflowFact]
    public async Task The_first_member_number_for_a_year_starts_at_0001_and_other_years_do_not_interfere()
    {
        using var rig = new Rig();
        var otherYear = Person("Active", 2010, "UMAT-2010-0099"); otherYear.Id = "other"; otherYear.Email = "o@x.com";
        await rig.Seed(Tx(), Camp(true, ThisYear), Person("Pending", 2015), otherYear, Institution());
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal("UMAT-2015-0001", (await db.Members.IgnoreQueryFilters().SingleAsync(x => x.Id == "m1")).MemberNumber);
    }

    [WorkflowFact]
    public async Task A_pending_member_who_already_has_a_number_keeps_it_when_approved()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp(true, ThisYear), Person("Pending", 2015, "KEEP-ME-1"), Institution());
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var m = await db.Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Active", "KEEP-ME-1"), (m.Status, m.MemberNumber));
    }

    [WorkflowFact]
    public async Task Under_the_approved_only_policy_dues_do_not_decide_activity_but_status_does()
    {
        using var rig = new Rig();
        var older = Camp(true, ThisYear - 1); older.Id = "c-old";
        await rig.Seed(Tx(), Camp(true, ThisYear), older, Person("Active", ThisYear - 1), Institution("ApprovedOnly"));
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        Assert.True((await db.Members.IgnoreQueryFilters().SingleAsync()).IsMembershipActive);   // arrears remain, but the policy ignores dues
    }

    [WorkflowFact]
    public async Task A_referred_member_who_pays_dues_moves_their_referral_to_membership_paid()
    {
        using var rig = new Rig();
        await rig.Seed(Tx(), Camp(true, ThisYear), Person(gradYear: ThisYear), Institution(),
            new Referral { Id = "ref1", ReferredMemberId = "m1", Status = "Registered", InstitutionId = Inst, ReferrerId = "boss" },
            new Referral { Id = "ref2", ReferredMemberId = "someone-else", Status = "Registered", InstitutionId = Inst, ReferrerId = "boss" });
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var statuses = await db.Referrals.IgnoreQueryFilters().ToDictionaryAsync(r => r.Id, r => r.Status);
        Assert.Equal(("MembershipPaid", "Registered"), (statuses["ref1"], statuses["ref2"]));
    }

    [WorkflowFact]
    public async Task A_non_membership_campaign_never_touches_the_members_membership_fields()
    {
        using var rig = new Rig();
        var person = Person(); person.IsMembershipActive = false; person.MembershipYearsPaid = 0;
        await rig.Seed(Tx(), Camp(false), person, Institution());
        rig.Verify(true, "success", 10390);

        await Run(rig);

        using var db = rig.Db();
        var m = await db.Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((false, 0, null), (m.IsMembershipActive, m.MembershipYearsPaid, m.LastMembershipPaidAt));
    }
}

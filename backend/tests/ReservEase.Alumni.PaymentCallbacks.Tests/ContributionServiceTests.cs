using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PaymentCallbacks.Tests;

public class ContributionServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IPaystackService> Paystack { get; } = new();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public InMemoryRedisService<MemberRedisConfig> Redis { get; } = new();
        public List<InitializePaymentRequest> Initialized { get; } = new();
        public AuthData Member { get; } = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public ContributionService Service { get; private set; } = null!;

        public Rig(Action<Institution>? institution = null, bool paystackAccepts = true)
        {
            Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()))
                .Callback<InitializePaymentRequest>(r => Initialized.Add(r))
                .ReturnsAsync(() => new InitializePaymentResponse
                {
                    Status = paystackAccepts, Message = paystackAccepts ? "ok" : "Declined",
                    Data = new InitializePaymentData { AuthorizationUrl = "https://paystack/pay" },
                });
            Temporal.SetupGet(t => t.IsAvailable).Returns(false);

            var inst = new Institution { Id = Tenant, Slug = "umat", Name = "UMaT", MemberActivePolicy = "DuesRequired" };
            institution?.Invoke(inst);
            using (var seed = TestDb.Create(DbName, Tenant)) { seed.Institutions.Add(inst); seed.SaveChanges(); }
            Fresh();
        }

        public ContributionService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new ContributionService(
                new AlumniPgRepository<Contribution>(db), new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<MemberEntity>(db),
                new AlumniPgRepository<PaymentTransaction>(db), new AlumniPgRepository<Institution>(db), new AlumniPgRepository<Batch>(db),
                new AlumniPgRepository<RecurringContribution>(db), new AlumniPgRepository<PlatformSettings>(db),
                TestDb.Tenant(Tenant, "umat"), Paystack.Object, new PaystackConfig { GatewayFeePercentage = 1.95m, GatewayFeeSafetyBufferSubunit = 2 },
                Redis, Temporal.Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PaystackConfig:CallbackUrl"] = "https://app.test/pay" }).Build(),
                NullLogger<ContributionService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Add(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }

        public Task<PaymentTransaction> OnlyTransaction() => Db().PaymentTransactions.SingleAsync();
    }

    private static Campaign Campaign(string id = "c1", decimal amountPerMember = 0, bool membership = false, int? year = null) => new()
    {
        Id = id, Title = $"Campaign {id}", Deadline = DateTime.UtcNow.AddDays(30), AmountPerMember = amountPerMember,
        IsMembershipCampaign = membership, MembershipYear = year, AllowManualPayments = true,
    };

    private static MemberEntity Person(string id = "m1", int gradYear = 2015, string status = "Active") =>
        new() { Id = id, FirstName = "Ama", LastName = "Mensah", Email = $"{id}@x.com", GraduationYear = gradYear, Status = status };

    private static string Prop(object? o, string name) => o!.GetType().GetProperty(name)!.GetValue(o)!.ToString()!;

    // ── One-off contributions ───────────────────────────────────────────

    [Fact]
    public async Task A_member_contribution_is_recorded_pending_and_paystack_is_initialised()
    {
        var rig = new Rig();
        await rig.Add(Campaign());

        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 150.75m), rig.Member);

        Assert.Equal(200, response.Code);
        var tx = await rig.OnlyTransaction();
        Assert.Equal(("Pending", 150.75m, "m1", "Paystack", false), (tx.Status, tx.Amount, tx.MemberId, tx.PaymentMethod, tx.IsGuestPayment));
        Assert.StartsWith("CN_", tx.Reference);
        Assert.Equal("Campaign c1", tx.Campaign!.Title);
        Assert.Equal(tx.Reference, Prop(response.Data, "reference"));
        Assert.Equal("https://paystack/pay", Prop(response.Data, "authorizationUrl"));

        var init = Assert.Single(rig.Initialized);
        Assert.Equal(15075, init.Amount);
        Assert.Equal("ama@x.com", init.Email);
        Assert.Equal(("m1", "c1"), (init.Metadata!["memberId"], init.Metadata["campaignId"]));
        Assert.Equal("https://app.test/pay/callback", init.CallbackUrl);
    }

    [Fact]
    public async Task The_reference_is_cached_in_redis_so_the_webhook_can_claim_it_before_the_workflow_writes_anything()
    {
        var rig = new Rig();
        await rig.Add(Campaign());

        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member);

        var reference = Prop(response.Data, "reference");
        Assert.Contains($"paystack:ref:{reference}", rig.Redis.Store.Keys);
    }

    [Fact]
    public async Task OwnsReference_is_true_for_a_saved_transaction_or_a_cached_reference_only()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        var reference = Prop((await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member)).Data, "reference");

        Assert.True(await rig.Service.OwnsReferenceAsync(reference));
        Assert.False(await rig.Service.OwnsReferenceAsync("CN_unknown"));

        await rig.Redis.SetAsync("paystack:ref:CN_cached_only", new { MemberId = "m", CampaignId = "c" });
        Assert.True(await rig.Service.OwnsReferenceAsync("CN_cached_only"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task A_non_positive_amount_is_rejected(double amount)
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", (decimal)amount), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Equal(0, await rig.Db().PaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task An_unknown_campaign_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("ghost", 10), rig.Member)).Code);
    }

    [Fact]
    public async Task Paying_after_the_deadline_is_blocked_only_when_the_platform_setting_is_on()
    {
        var rig = new Rig();
        var late = Campaign(); late.Deadline = DateTime.UtcNow.AddDays(-1);
        await rig.Add(late);

        Assert.Equal(200, (await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member)).Code);

        await rig.Add(new PlatformSettings { Id = PlatformSettings.SingletonId, BlockOverdueCampaignPayments = true });
        var blocked = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member);
        Assert.Equal(400, blocked.Code);
        Assert.Contains("deadline has passed", blocked.Message);
    }

    [Fact]
    public async Task A_campaign_before_its_deadline_is_never_blocked_even_with_the_setting_on()
    {
        var rig = new Rig();
        await rig.Add(Campaign(), new PlatformSettings { Id = PlatformSettings.SingletonId, BlockOverdueCampaignPayments = true });
        Assert.Equal(200, (await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member)).Code);
    }

    // ── Guests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_guest_payment_is_flagged_and_uses_the_email_provided()
    {
        var rig = new Rig();
        await rig.Add(Campaign());

        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, Email: "guest@x.com", ShowOnWallOfSupport: true), null);

        Assert.Equal(200, response.Code);
        var tx = await rig.OnlyTransaction();
        Assert.Equal((true, string.Empty, true), (tx.IsGuestPayment, tx.MemberId, tx.ShowOnWallOfSupport));
        Assert.Equal("anonymous", tx.CreatedBy);
        Assert.Equal("guest@x.com", rig.Initialized.Single().Email);
    }

    [Fact]
    public async Task A_guest_without_an_email_gets_an_anonymised_but_valid_looking_address()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20), null);

        var email = rig.Initialized.Single().Email;
        Assert.StartsWith("guest+", email);
        Assert.EndsWith("@guest.alumunion.com", email);
        Assert.DoesNotContain(".invalid", email);
    }

    [Fact]
    public async Task A_guest_whose_email_matches_a_pending_member_is_linked_to_that_member()
    {
        var rig = new Rig();
        var pending = Person("pending-1", status: "Pending"); pending.Email = "wait@x.com";
        await rig.Add(Campaign(), pending);

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, Email: "WAIT@x.com "), null);

        var tx = await rig.OnlyTransaction();
        Assert.Equal("pending-1", tx.MemberId);
        Assert.True(tx.IsGuestPayment);   // still recorded as the guest flow it started as
    }

    [Fact]
    public async Task An_active_members_email_is_not_used_to_link_a_guest_payment()
    {
        var rig = new Rig();
        var active = Person("active-1"); active.Email = "active@x.com";
        await rig.Add(Campaign(), active);

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, Email: "active@x.com"), null);

        Assert.Equal(string.Empty, (await rig.OnlyTransaction()).MemberId);
    }

    [Fact]
    public async Task A_guest_arriving_through_a_shared_link_is_attributed_to_the_sharer()
    {
        var rig = new Rig();
        await rig.Add(Campaign(), Person("sharer"));

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, SharedByMemberId: "sharer"), null);

        var tx = await rig.OnlyTransaction();
        Assert.Equal(("sharer", "sharer", true), (tx.MemberId, tx.SharedByMemberId, tx.IsGuestPayment));
    }

    [Fact]
    public async Task An_unknown_sharer_leaves_the_payment_anonymous()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, SharedByMemberId: "nobody"), null);
        Assert.Equal(string.Empty, (await rig.OnlyTransaction()).MemberId);
    }

    // ── Recurring giving ────────────────────────────────────────────────

    [Fact]
    public async Task Monthly_giving_requires_being_signed_in()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, SetupRecurringGiving: true), null);
        Assert.Equal(400, response.Code);
        Assert.Contains("logged in", response.Message);
    }

    [Fact]
    public async Task Monthly_giving_is_blocked_when_the_institution_disabled_the_feature()
    {
        var rig = new Rig(i => i.DisabledFeatures = [InstitutionFeatures.RecurringGiving]);
        await rig.Add(Campaign());
        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, SetupRecurringGiving: true), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("not enabled", response.Message);
    }

    [Fact]
    public async Task Monthly_giving_is_recorded_on_the_transaction_and_the_cached_reference()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 20, SetupRecurringGiving: true), rig.Member);
        Assert.True((await rig.OnlyTransaction()).SetupRecurringGiving);
    }

    // ── Fees and subaccount routing ─────────────────────────────────────

    [Fact]
    public async Task With_a_subaccount_the_payer_covers_both_fees_and_the_split_is_sent_to_paystack()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = "ACCT_INST"; i.PlatformFeePercentage = 2.5m; });
        await rig.Add(Campaign());

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 400), rig.Member);

        var expected = PaystackFeeCalculator.CalculateZeroDeductionCharge(40000, 2.5m, 1.95m, 0, null, 2);
        var init = rig.Initialized.Single();
        var tx = await rig.OnlyTransaction();
        Assert.Equal((expected.ChargeAmountSubunit, expected.TransactionChargeSubunit, "account", "ACCT_INST"), (init.Amount, init.TransactionCharge, init.Bearer, init.Subaccount));
        Assert.Equal(400m, tx.Amount);   // the institution's share is untouched
        Assert.Equal(tx.Amount + tx.PlatformFeeAmount + tx.GatewayFeeAmount, tx.GrossChargeAmount);
        Assert.Equal(expected.TransactionChargeSubunit / 100m, tx.TransactionChargeAmount);
    }

    [Fact]
    public async Task Without_a_subaccount_the_charge_is_the_plain_amount_with_no_split()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 99.99m), rig.Member);

        var init = rig.Initialized.Single();
        Assert.Equal((9999L, (long?)null, (string?)null, (string?)null), (init.Amount, init.TransactionCharge, init.Bearer, init.Subaccount));
        var tx = await rig.OnlyTransaction();
        Assert.Equal((0m, 0m, 99.99m), (tx.PlatformFeeAmount, tx.GatewayFeeAmount, tx.GrossChargeAmount));
    }

    private static Batch ApprovedBatch(int year, string code = "ACCT_BATCH") =>
        new() { Id = $"b{year}", Name = $"Class of {year}", Year = year, PayoutStatus = "Approved", UseInstitutionAccount = false, PaystackSubaccountCode = code };

    [Fact]
    public async Task A_single_batch_campaign_settles_into_that_batchs_approved_subaccount_but_the_institution_fee_applies()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = "ACCT_INST"; i.PlatformFeePercentage = 2m; });
        var campaign = Campaign(); campaign.YearGroups = [2018];
        await rig.Add(campaign, ApprovedBatch(2018));

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 100), rig.Member);

        var init = rig.Initialized.Single();
        Assert.Equal("ACCT_BATCH", init.Subaccount);
        Assert.Equal(PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 2m, 1.95m, 0, null, 2).ChargeAmountSubunit, init.Amount);
    }

    [Theory]
    [InlineData("Pending", false)]
    [InlineData("None", false)]
    [InlineData("Approved", true)]   // but UseInstitutionAccount below flips it
    public async Task Only_an_approved_batch_payout_account_is_used(string payoutStatus, bool usesBatch)
    {
        var rig = new Rig(i => i.PaystackSubaccountCode = "ACCT_INST");
        var campaign = Campaign(); campaign.YearGroups = [2018];
        var batch = ApprovedBatch(2018); batch.PayoutStatus = payoutStatus;
        await rig.Add(campaign, batch);

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 100), rig.Member);

        Assert.Equal(usesBatch ? "ACCT_BATCH" : "ACCT_INST", rig.Initialized.Single().Subaccount);
    }

    [Fact]
    public async Task A_batch_that_opted_to_use_the_institution_account_falls_back_to_it()
    {
        var rig = new Rig(i => i.PaystackSubaccountCode = "ACCT_INST");
        var campaign = Campaign(); campaign.YearGroups = [2018];
        var batch = ApprovedBatch(2018); batch.UseInstitutionAccount = true;
        await rig.Add(campaign, batch);

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 100), rig.Member);

        Assert.Equal("ACCT_INST", rig.Initialized.Single().Subaccount);
    }

    [Fact]
    public async Task A_campaign_covering_several_batches_always_uses_the_institution_account()
    {
        var rig = new Rig(i => i.PaystackSubaccountCode = "ACCT_INST");
        var campaign = Campaign(); campaign.YearGroups = [2018, 2019];
        await rig.Add(campaign, ApprovedBatch(2018), ApprovedBatch(2019));

        await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 100), rig.Member);

        Assert.Equal("ACCT_INST", rig.Initialized.Single().Subaccount);
    }

    // ── Paystack outcomes ───────────────────────────────────────────────

    [Fact]
    public async Task When_paystack_declines_the_transaction_is_failed_and_the_cached_reference_removed()
    {
        var rig = new Rig(paystackAccepts: false);
        await rig.Add(Campaign());

        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member);

        Assert.Equal(400, response.Code);
        var tx = await rig.OnlyTransaction();
        Assert.Equal(("Failed", "Declined"), (tx.Status, tx.FailureMessage));
        Assert.DoesNotContain($"paystack:ref:{tx.Reference}", rig.Redis.Store.Keys);
    }

    [Fact]
    public async Task An_exception_becomes_a_500()
    {
        var rig = new Rig();
        await rig.Add(Campaign());
        rig.Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>())).ThrowsAsync(new InvalidOperationException());
        Assert.Equal(500, (await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("c1", 10), rig.Member)).Code);
    }

    // ── Membership dues ─────────────────────────────────────────────────

    [Fact]
    public async Task Paying_a_membership_campaign_through_the_normal_endpoint_routes_to_renewal_and_uses_the_campaigns_amount_not_the_requested_one()
    {
        var rig = new Rig();
        await rig.Add(Campaign("dues", amountPerMember: 120, membership: true, year: 2026), Person());

        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("dues", 1), rig.Member);

        Assert.Equal(200, response.Code);
        Assert.Equal(12000, rig.Initialized.Single().Amount);
        Assert.Equal("1", rig.Initialized.Single().Metadata!["membershipYears"]);
        Assert.Equal(1, (await rig.OnlyTransaction()).MembershipYears);
    }

    [Fact]
    public async Task A_guest_cannot_pay_membership_dues()
    {
        var rig = new Rig();
        await rig.Add(Campaign("dues", 120, true, 2026));
        var response = await rig.Service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("dues", 1, Email: "guest@x.com"), null);
        Assert.Equal(400, response.Code);
        Assert.Contains("logged in", response.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Renewal_is_exactly_one_year(int years)
    {
        var rig = new Rig();
        await rig.Add(Campaign("dues", 120, true, 2026), Person());
        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", years), rig.Member);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task Renewal_of_a_campaign_that_is_not_a_membership_campaign_is_404()
    {
        var rig = new Rig();
        await rig.Add(Campaign("plain"), Person());
        Assert.Equal(404, (await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("plain", 1), rig.Member)).Code);
        Assert.Equal(404, (await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("ghost", 1), rig.Member)).Code);
    }

    [Fact]
    public async Task A_member_cannot_pay_the_same_membership_campaign_twice()
    {
        var rig = new Rig();
        await rig.Add(Campaign("dues", 120, true, 2026), Person(),
            new Contribution { Id = "paid", CampaignId = "dues", MemberId = "m1", Status = "Successful", Amount = 120 });

        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Contains("already paid", response.Message);
    }

    [Fact]
    public async Task A_pending_or_failed_earlier_attempt_does_not_count_as_paid()
    {
        var rig = new Rig();
        await rig.Add(Campaign("dues", 120, true, 2026), Person(),
            new Contribution { Id = "x", CampaignId = "dues", MemberId = "m1", Status = "Pending" },
            new Contribution { Id = "y", CampaignId = "dues", MemberId = "m1", Status = "Failed" });
        Assert.Equal(200, (await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1), rig.Member)).Code);
    }

    [Fact]
    public async Task Dues_for_a_year_before_the_members_graduation_are_refused()
    {
        var rig = new Rig();
        await rig.Add(Campaign("old", 120, true, 2010), Person(gradYear: 2015));
        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("old", 1), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("before your graduation", response.Message);
    }

    [Fact]
    public async Task Pensioners_pay_the_pensioner_rate_when_the_campaign_has_one()
    {
        var rig = new Rig();
        var campaign = Campaign("dues", 120, true, 2026); campaign.PensionerAmountPerMember = 60;
        var pensioner = Person(); pensioner.EmploymentStatus = "Pensioner";
        await rig.Add(campaign, pensioner);

        await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1), rig.Member);

        Assert.Equal(6000, rig.Initialized.Single().Amount);
    }

    [Fact]
    public async Task A_pensioner_pays_the_standard_rate_when_no_pensioner_rate_is_set()
    {
        var rig = new Rig();
        var pensioner = Person(); pensioner.EmploymentStatus = "Pensioner";
        await rig.Add(Campaign("dues", 120, true, 2026), pensioner);
        await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1), rig.Member);
        Assert.Equal(12000, rig.Initialized.Single().Amount);
    }

    [Fact]
    public async Task Manual_renewal_creates_a_pending_contribution_and_returns_the_bank_details_without_calling_paystack()
    {
        var rig = new Rig();
        var campaign = Campaign("dues", 120, true, 2026);
        campaign.BankAccount = new ManualPaymentBankAccount { AccountNumber = "0123", AccountName = "UMaT", BankName = "GCB" };
        await rig.Add(campaign, Person());

        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1, "MANUAL"), rig.Member);

        Assert.Equal(200, response.Code);
        var c = await rig.Db().Contributions.SingleAsync();
        Assert.Equal(("Pending", "Manual", 120m), (c.Status, c.PaymentMethod, c.Amount));
        Assert.Equal(c.Id, Prop(response.Data, "contributionId"));
        rig.Paystack.Verify(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task Manual_renewal_is_refused_when_the_campaign_does_not_allow_it()
    {
        var rig = new Rig();
        var campaign = Campaign("dues", 120, true, 2026); campaign.AllowManualPayments = false;
        await rig.Add(campaign, Person());
        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1, "manual"), rig.Member);
        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task An_unknown_payment_method_is_rejected()
    {
        var rig = new Rig();
        await rig.Add(Campaign("dues", 120, true, 2026), Person());
        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1, "crypto"), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("Invalid payment method", response.Message);
    }

    [Fact]
    public async Task Renewal_with_a_subaccount_charges_the_grossed_up_amount()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = "ACCT_INST"; i.PlatformFeePercentage = 1.5m; });
        await rig.Add(Campaign("dues", 120, true, 2026), Person());

        await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1), rig.Member);

        var expected = PaystackFeeCalculator.CalculateZeroDeductionCharge(12000, 1.5m, 1.95m, 0, null, 2);
        Assert.Equal(expected.ChargeAmountSubunit, rig.Initialized.Single().Amount);
        Assert.Equal("account", rig.Initialized.Single().Bearer);
    }

    [Fact]
    public async Task Renewal_declined_by_paystack_marks_the_transaction_failed()
    {
        var rig = new Rig(paystackAccepts: false);
        await rig.Add(Campaign("dues", 120, true, 2026), Person());
        var response = await rig.Service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("dues", 1), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Equal("Failed", (await rig.OnlyTransaction()).Status);
    }

    // ── Membership status ───────────────────────────────────────────────

    private static readonly int ThisYear = DateTime.UtcNow.Year;

    [Fact]
    public async Task Status_for_an_unknown_member_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.GetMembershipStatusAsync(new AuthData { Id = "ghost" })).Code);
    }

    [Fact]
    public async Task With_the_current_year_paid_the_member_is_active_until_year_end_with_no_arrears()
    {
        var rig = new Rig();
        await rig.Add(Person(gradYear: ThisYear - 1), Campaign("d-now", 100, true, ThisYear), Campaign("d-prev", 100, true, ThisYear - 1),
            new Contribution { Id = "p1", CampaignId = "d-now", MemberId = "m1", Status = "Successful" },
            new Contribution { Id = "p2", CampaignId = "d-prev", MemberId = "m1", Status = "Successful" });

        var status = (await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!;

        Assert.True(status.IsMembershipActive);
        Assert.True(status.IsCurrentYearPaid);
        Assert.False(status.HasArrears);
        Assert.Equal(2, status.MembershipYearsPaid);
        Assert.Equal(new DateTime(ThisYear, 12, 31, 23, 59, 59, DateTimeKind.Utc), status.MembershipExpiry);
    }

    [Fact]
    public async Task Unpaid_past_years_are_arrears_listed_newest_first_but_do_not_make_a_paid_member_inactive()
    {
        var rig = new Rig();
        await rig.Add(Person(gradYear: ThisYear - 3), Campaign("a", 100, true, ThisYear), Campaign("b", 100, true, ThisYear - 1),
            Campaign("c", 100, true, ThisYear - 2), Campaign("d", 100, true, ThisYear - 3),
            new Contribution { Id = "p", CampaignId = "a", MemberId = "m1", Status = "Successful" },
            new Contribution { Id = "q", CampaignId = "d", MemberId = "m1", Status = "Successful" });

        var status = (await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!;

        Assert.True(status.IsMembershipActive);
        Assert.True(status.HasArrears);
        Assert.Equal(2, status.ArrearsCount);
        Assert.Equal(new[] { ThisYear - 1, ThisYear - 2 }, status.ArrearsYears);
    }

    [Fact]
    public async Task An_unpaid_current_year_under_the_dues_policy_makes_the_member_inactive_with_no_expiry()
    {
        var rig = new Rig();   // policy: DuesRequired
        await rig.Add(Person(gradYear: ThisYear - 1), Campaign("a", 100, true, ThisYear));

        var status = (await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!;

        Assert.False(status.IsMembershipActive);
        Assert.False(status.IsCurrentYearPaid);
        Assert.Null(status.MembershipExpiry);
        Assert.Equal("DuesRequired", status.ActivePolicy);
    }

    [Fact]
    public async Task Under_the_approved_only_policy_an_active_member_is_active_even_with_dues_unpaid()
    {
        var rig = new Rig(i => i.MemberActivePolicy = "ApprovedOnly");
        await rig.Add(Person(gradYear: ThisYear - 1), Campaign("a", 100, true, ThisYear));

        var status = (await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!;

        Assert.True(status.IsMembershipActive);
        Assert.False(status.IsCurrentYearPaid);
        Assert.Equal("ApprovedOnly", status.ActivePolicy);
    }

    [Fact]
    public async Task Under_the_approved_only_policy_a_pending_member_is_inactive_even_if_dues_are_paid()
    {
        var rig = new Rig(i => i.MemberActivePolicy = "ApprovedOnly");
        await rig.Add(Person(gradYear: ThisYear - 1, status: "Pending"), Campaign("a", 100, true, ThisYear),
            new Contribution { Id = "p", CampaignId = "a", MemberId = "m1", Status = "Successful" });

        Assert.False((await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!.IsMembershipActive);
    }

    [Fact]
    public async Task Years_before_graduation_and_in_the_future_are_not_owed()
    {
        var rig = new Rig();
        await rig.Add(Person(gradYear: ThisYear), Campaign("old", 100, true, ThisYear - 2), Campaign("future", 100, true, ThisYear + 1),
            Campaign("now", 100, true, ThisYear),
            new Contribution { Id = "p", CampaignId = "now", MemberId = "m1", Status = "Successful" });

        var status = (await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!;

        Assert.False(status.HasArrears);
        Assert.Equal(1, status.MembershipYearsPaid);
    }

    [Fact]
    public async Task Without_any_membership_campaigns_status_falls_back_to_the_member_record()
    {
        var rig = new Rig();
        var expiry = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var person = Person(); person.IsMembershipActive = true; person.MembershipExpiry = expiry; person.MembershipYearsPaid = 4;
        await rig.Add(person);

        var status = (await rig.Service.GetMembershipStatusAsync(rig.Member)).Data!;

        Assert.True(status.IsMembershipActive);
        Assert.Equal((expiry, 4, false), (status.MembershipExpiry, status.MembershipYearsPaid, status.HasArrears));
    }

    [Fact]
    public async Task Unpaid_membership_campaigns_for_this_member_are_listed_and_paid_ones_are_not()
    {
        var rig = new Rig();
        await rig.Add(Person(gradYear: ThisYear - 2), Campaign("a", 100, true, ThisYear), Campaign("b", 100, true, ThisYear - 1), Campaign("c", 100, true, ThisYear - 2),
            Campaign("plain"), Campaign("future", 100, true, ThisYear + 1),
            new Contribution { Id = "p", CampaignId = "b", MemberId = "m1", Status = "Successful", Campaign = new CampaignSnapshot { Id = "b", Title = "b" } });

        var unpaid = (await rig.Service.GetCurrentYearUnpaidMembershipCampaignsAsync(rig.Member)).Data!;

        Assert.Equal(new[] { "a", "c" }, unpaid.Select(c => c.Id).OrderBy(x => x));
    }

    // ── Contribution lists and summary ──────────────────────────────────

    [Fact]
    public async Task My_contributions_are_scoped_to_the_member_filterable_by_campaign_and_newest_first()
    {
        var rig = new Rig();
        var snap = new MemberSnapshot { Id = "m1", FirstName = "Old", LastName = "Name" };
        await rig.Add(Person(),
            new Contribution { Id = "1", CampaignId = "a", MemberId = "m1", Amount = 10, CreatedAt = DateTime.UtcNow.AddDays(-2), Member = snap, Campaign = new CampaignSnapshot { Id = "a", Title = "A" } },
            new Contribution { Id = "2", CampaignId = "b", MemberId = "m1", Amount = 20, CreatedAt = DateTime.UtcNow, Member = snap, Campaign = new CampaignSnapshot { Id = "b", Title = "B" } },
            new Contribution { Id = "3", CampaignId = "a", MemberId = "other", Amount = 30, Member = snap, Campaign = new CampaignSnapshot { Id = "a", Title = "A" } });

        var all = await rig.Service.GetMyContributionsAsync("m1", new ContributionFilter());
        Assert.Equal(new[] { "2", "1" }, all.Data!.Results.Select(c => c.Id));
        Assert.All(all.Data.Results, c => Assert.Equal("Ama Mensah", c.MemberName));   // refreshed from the live member record

        var onlyA = await rig.Service.GetMyContributionsAsync("m1", new ContributionFilter { CampaignId = "a" });
        Assert.Equal(new[] { "1" }, onlyA.Data!.Results.Select(c => c.Id));
    }

    [Fact]
    public async Task Missing_snapshots_are_backfilled_and_persisted()
    {
        var rig = new Rig();
        await rig.Add(Person(), Campaign("a"), new Contribution { Id = "1", CampaignId = "a", MemberId = "m1", Amount = 10 });

        var response = await rig.Service.GetMyContributionsAsync("m1", new ContributionFilter());

        Assert.Equal("Campaign a", response.Data!.Results.Single().CampaignTitle);
        var saved = await rig.Db().Contributions.SingleAsync();
        Assert.NotNull(saved.Member);
        Assert.Equal("Campaign a", saved.Campaign!.Title);
    }

    [Fact]
    public async Task The_summary_counts_only_successful_payments_and_splits_this_year()
    {
        var rig = new Rig();
        await rig.Add(
            new Contribution { Id = "1", CampaignId = "a", MemberId = "m1", Amount = 100, Status = "Successful", CreatedAt = DateTime.UtcNow },
            new Contribution { Id = "2", CampaignId = "b", MemberId = "m1", Amount = 50, Status = "Successful", CreatedAt = new DateTime(ThisYear - 1, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Contribution { Id = "3", CampaignId = "a", MemberId = "m1", Amount = 25, Status = "Successful", CreatedAt = DateTime.UtcNow },
            new Contribution { Id = "4", CampaignId = "c", MemberId = "m1", Amount = 999, Status = "Pending" },
            new Contribution { Id = "5", CampaignId = "d", MemberId = "m1", Amount = 999, Status = "Failed" },
            new Contribution { Id = "6", CampaignId = "e", MemberId = "other", Amount = 999, Status = "Successful" });

        var summary = (await rig.Service.GetMyContributionSummaryAsync("m1")).Data!;

        Assert.Equal((175m, 125m), (summary.TotalPaid, summary.TotalPaidThisYear));
        Assert.Equal(new[] { "a", "b" }, summary.PaidCampaignIds.OrderBy(x => x));
    }

    [Fact]
    public async Task The_summary_for_a_member_with_nothing_is_zeros()
    {
        var summary = (await new Rig().Service.GetMyContributionSummaryAsync("nobody")).Data!;
        Assert.Equal((0m, 0m), (summary.TotalPaid, summary.TotalPaidThisYear));
        Assert.Empty(summary.PaidCampaignIds);
    }

    // ── Payment status ──────────────────────────────────────────────────

    private static async Task<(Rig rig, string reference)> WithTransaction(string status, string memberId = "m1", string? failure = null, decimal amount = 75)
    {
        var rig = new Rig();
        var reference = "CN_" + Guid.NewGuid().ToString("N");
        await rig.Add(new PaymentTransaction { Id = "t1", Reference = reference, MemberId = memberId, Status = status, Amount = amount, PaymentMethod = "Paystack", FailureMessage = failure,
            Member = new MemberSnapshot { Id = memberId, Email = "snap@x.com" } });
        return (rig, reference);
    }

    [Fact]
    public async Task A_pending_transaction_reports_pending_without_an_amount()
    {
        var (rig, reference) = await WithTransaction("Pending");
        var status = (await rig.Service.GetContributionStatusAsync(reference, rig.Member)).Data!;
        Assert.Equal("Pending", status.Status);
        Assert.Null(status.Amount);
        Assert.Contains("not yet completed", status.Message);
    }

    [Fact]
    public async Task A_successful_transaction_reports_amount_method_and_a_confirmation()
    {
        var (rig, reference) = await WithTransaction("Successful", amount: 88.5m);
        var status = (await rig.Service.GetContributionStatusAsync(reference, rig.Member)).Data!;
        Assert.Equal(("Successful", 88.5m, "Paystack", "Payment confirmed"), (status.Status, status.Amount, status.PaymentMethod, status.Message));
    }

    [Fact]
    public async Task A_failed_transaction_reports_its_reason_or_a_default()
    {
        var (rig, reference) = await WithTransaction("Failed", failure: "Insufficient funds");
        Assert.Equal("Insufficient funds", (await rig.Service.GetContributionStatusAsync(reference, rig.Member)).Data!.Message);

        var (rig2, reference2) = await WithTransaction("Failed");
        Assert.Equal("Payment failed or rejected", (await rig2.Service.GetContributionStatusAsync(reference2, rig2.Member)).Data!.Message);
    }

    [Fact]
    public async Task A_signed_in_member_cannot_see_someone_elses_payment_status()
    {
        var (rig, reference) = await WithTransaction("Successful", memberId: "someone-else");
        var response = await rig.Service.GetContributionStatusAsync(reference, rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task An_anonymous_caller_who_knows_the_reference_may_check_a_guest_payment()
    {
        var (rig, reference) = await WithTransaction("Successful", memberId: "");
        Assert.Equal("Successful", (await rig.Service.GetContributionStatusAsync(reference, null)).Data!.Status);
    }

    [Fact]
    public async Task A_legacy_contribution_row_is_reported_when_there_is_no_transaction()
    {
        var rig = new Rig();
        await rig.Add(new Contribution { Id = "c", TransactionRef = "LEGACY", MemberId = "m1", Status = "Successful", Amount = 40, PaymentMethod = "Paystack" });

        var status = (await rig.Service.GetContributionStatusAsync("LEGACY", rig.Member)).Data!;

        Assert.Equal(("Successful", 40m, "Payment confirmed"), (status.Status, status.Amount, status.Message));
    }

    [Fact]
    public async Task A_legacy_contribution_belonging_to_someone_else_is_not_revealed()
    {
        var rig = new Rig();
        await rig.Add(new Contribution { Id = "c", TransactionRef = "LEGACY", MemberId = "other", Status = "Successful" });
        var status = (await rig.Service.GetContributionStatusAsync("LEGACY", rig.Member)).Data!;
        Assert.Equal("Pending", status.Status);
    }

    [Fact]
    public async Task An_unknown_reference_with_temporal_down_reports_pending_with_a_retry_message()
    {
        var rig = new Rig();
        var status = (await rig.Service.GetContributionStatusAsync("CN_nothing", rig.Member)).Data!;
        Assert.Equal("Pending", status.Status);
        Assert.Contains("try again", status.Message);
    }

    // ── Verify and activation ───────────────────────────────────────────

    [Fact]
    public async Task Verification_is_a_503_style_server_error_while_temporal_is_down()
    {
        var rig = new Rig();
        var response = await rig.Service.VerifyPaystackPaymentAsync("CN_x", rig.Member);
        Assert.Equal(500, response.Code);
        Assert.Contains("temporarily unavailable", response.Message);
    }

    [Fact]
    public async Task Activation_status_for_an_unknown_reference_is_pending_not_an_error()
    {
        var response = await new Rig().Service.GetActivationStatusAsync("CN_missing");
        Assert.Equal(200, response.Code);
        Assert.Equal("Pending", response.Data!.Status);
        Assert.Null(response.Data.MemberNumber);
    }

    [Fact]
    public async Task A_successful_activation_surfaces_the_member_number_only_once_the_member_is_active()
    {
        var rig = new Rig();
        var person = Person("m9"); person.MemberNumber = "UMAT-2015-0001"; person.Email = "m9@x.com";
        await rig.Add(person, new PaymentTransaction { Reference = "CN_ok", MemberId = "m9", Status = "Successful" });

        var data = (await rig.Service.GetActivationStatusAsync("CN_ok")).Data!;
        Assert.Equal(("Successful", "UMAT-2015-0001"), (data.Status, data.MemberNumber));
        Assert.Contains("activated", data.Message);

        var pendingPerson = Person("m10", status: "Pending"); pendingPerson.MemberNumber = "UMAT-2015-0002";
        await rig.Add(pendingPerson, new PaymentTransaction { Reference = "CN_pending", MemberId = "m10", Status = "Successful" });
        Assert.Null((await rig.Service.GetActivationStatusAsync("CN_pending")).Data!.MemberNumber);
    }

    [Theory]
    [InlineData("Failed", "Your card was declined", "Your card was declined")]
    [InlineData("Failed", null, "Payment could not be completed. Please try again.")]
    [InlineData("Pending", null, "Your payment is still being processed. This usually takes a few seconds.")]
    public async Task Activation_status_messages_per_state(string state, string? failure, string message)
    {
        var rig = new Rig();
        await rig.Add(new PaymentTransaction { Reference = "CN_s", MemberId = "", Status = state, FailureMessage = failure });
        var data = (await rig.Service.GetActivationStatusAsync("CN_s")).Data!;
        Assert.Equal((state, message), (data.Status, data.Message));
    }

    [Fact]
    public async Task Activation_status_takes_the_email_from_the_transaction_snapshot_first_then_the_member()
    {
        var rig = new Rig();
        var person = Person("m11"); person.Email = "live@x.com";
        await rig.Add(person,
            new PaymentTransaction { Reference = "CN_snap", MemberId = "m11", Status = "Pending", Member = new MemberSnapshot { Email = "snap@x.com" } },
            new PaymentTransaction { Reference = "CN_nosnap", MemberId = "m11", Status = "Pending" });

        Assert.Equal("snap@x.com", (await rig.Service.GetActivationStatusAsync("CN_snap")).Data!.Email);
        Assert.Equal("live@x.com", (await rig.Service.GetActivationStatusAsync("CN_nosnap")).Data!.Email);
    }

    // ── Manual proof ────────────────────────────────────────────────────

    [Fact]
    public async Task Uploading_proof_creates_a_pending_zero_amount_manual_contribution_awaiting_confirmation()
    {
        var rig = new Rig();
        await rig.Add(Campaign(), Person());

        var response = await rig.Service.UploadProofAsync(new UploadContributionProofRequest("c1", "BANK-REF-1", "Paid at the branch"), rig.Member);

        Assert.Equal(201, response.Code);
        var c = await rig.Db().Contributions.SingleAsync();
        Assert.Equal(("Pending", "Manual", 0m, "BANK-REF-1", "Paid at the branch"), (c.Status, c.PaymentMethod, c.Amount, c.TransactionRef, c.Notes));
        Assert.Equal("m1", c.MemberId);
        Assert.Equal("Campaign c1", c.Campaign!.Title);
    }

    [Fact]
    public async Task Proof_for_an_unknown_campaign_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.UploadProofAsync(new UploadContributionProofRequest("ghost", "R", null), rig.Member)).Code);
    }

    [Fact]
    public async Task Proof_for_a_membership_year_before_graduation_is_refused()
    {
        var rig = new Rig();
        await rig.Add(Campaign("old", 100, true, 2010), Person(gradYear: 2015));
        var response = await rig.Service.UploadProofAsync(new UploadContributionProofRequest("old", "R", null), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Equal(0, await rig.Db().Contributions.CountAsync());
    }

    // ── Recurring giving ────────────────────────────────────────────────

    [Fact]
    public async Task My_recurring_gifts_are_scoped_to_the_member_newest_first()
    {
        var rig = new Rig();
        await rig.Add(
            new RecurringContribution { Id = "old", MemberId = "m1", CampaignId = "c", CreatedAt = DateTime.UtcNow.AddDays(-5), Amount = 10 },
            new RecurringContribution { Id = "new", MemberId = "m1", CampaignId = "c", CreatedAt = DateTime.UtcNow, Amount = 20 },
            new RecurringContribution { Id = "other", MemberId = "m2", CampaignId = "c" });

        var gifts = (await rig.Service.GetMyRecurringGivingAsync("m1")).Data!;

        Assert.Equal(new[] { "new", "old" }, gifts.Select(g => g.Id));
    }

    [Fact]
    public async Task Cancelling_a_recurring_gift_is_terminal_and_stamps_who_did_it()
    {
        var rig = new Rig();
        await rig.Add(new RecurringContribution { Id = "r1", MemberId = "m1", CampaignId = "c", Status = "Active" });

        var response = await rig.Service.CancelRecurringGivingAsync("r1", "m1");

        Assert.Equal(200, response.Code);
        var saved = await rig.Db().RecurringContributions.SingleAsync();
        Assert.Equal(("Cancelled", "m1"), (saved.Status, saved.UpdatedBy));
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Failed")]
    public async Task A_gift_that_is_already_stopped_cannot_be_cancelled_again(string status)
    {
        var rig = new Rig();
        await rig.Add(new RecurringContribution { Id = "r1", MemberId = "m1", CampaignId = "c", Status = status });
        Assert.Equal(400, (await rig.Service.CancelRecurringGivingAsync("r1", "m1")).Code);
    }

    [Fact]
    public async Task A_member_cannot_cancel_someone_elses_recurring_gift()
    {
        var rig = new Rig();
        await rig.Add(new RecurringContribution { Id = "r1", MemberId = "m2", CampaignId = "c", Status = "Active" });

        Assert.Equal(404, (await rig.Service.CancelRecurringGivingAsync("r1", "m1")).Code);
        Assert.Equal("Active", (await rig.Db().RecurringContributions.SingleAsync()).Status);
        Assert.Equal(404, (await rig.Service.CancelRecurringGivingAsync("ghost", "m1")).Code);
    }
}

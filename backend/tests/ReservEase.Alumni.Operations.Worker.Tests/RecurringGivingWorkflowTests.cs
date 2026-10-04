using Microsoft.EntityFrameworkCore;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Temporalio.Client;
using Temporalio.Worker;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class RecurringGivingWorkflowTests(TemporalFixture temporal)
{
    private const string Inst = "i1";

    private async Task Run(ScheduledJobsRig rig)
    {
        var queue = "q-" + Guid.NewGuid().ToString("N");
        using var worker = new TemporalWorker(temporal.Client,
            new TemporalWorkerOptions(queue).AddWorkflow<RecurringGivingWorkflow>().AddAllActivities(rig.Activities));
        await worker.ExecuteAsync(() => temporal.Client.ExecuteWorkflowAsync(
            (RecurringGivingWorkflow wf) => wf.RunAsync(), new WorkflowOptions("wf-" + Guid.NewGuid().ToString("N"), queue)));
    }

    private static Institution Institution(Action<Institution>? tweak = null)
    {
        var i = new Institution { Id = Inst, Name = "UMaT", Slug = "umat", Status = "Active" };
        tweak?.Invoke(i);
        return i;
    }

    private static Campaign Camp(CampaignStatus status = CampaignStatus.Active, List<int>? years = null) =>
        new() { Id = "c1", Title = "Library", InstitutionId = Inst, Status = status, YearGroups = years, Deadline = DateTime.UtcNow.AddDays(30) };

    private static MemberEntity Person(string email = "ama@x.com") =>
        new() { Id = "m1", InstitutionId = Inst, FirstName = "Ama", LastName = "M", Email = email, Status = "Active" };

    private static RecurringContribution Gift(Action<RecurringContribution>? tweak = null)
    {
        var g = new RecurringContribution
        {
            Id = "r1", InstitutionId = Inst, MemberId = "m1", CampaignId = "c1", Amount = 50, Status = "Active", AuthorizationCode = "AUTH_1",
            NextChargeDate = DateTime.UtcNow.AddMinutes(-5), Member = new MemberSnapshot { Id = "m1", FirstName = "Ama", Email = "ama@x.com" },
        };
        tweak?.Invoke(g);
        return g;
    }

    private static void Charge(ScheduledJobsRig rig, bool ok, string status = "success", long amount = 5000, long? fees = 100, string message = "declined", string? gatewayResponse = null) =>
        rig.Paystack.Setup(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>())).ReturnsAsync(new ChargeAuthorizationResponse
        {
            Status = ok, Message = message,
            Data = new VerifyPaymentData { Status = status, Amount = amount, Fees = fees, GatewayResponse = gatewayResponse ?? "" },
        });

    [WorkflowFact]
    public async Task A_due_gift_is_charged_recorded_and_rescheduled_a_month_ahead()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(), Gift());
        Charge(rig, true, amount: 5000, fees: 100);

        await Run(rig);

        using var db = rig.Db();
        var c = await db.Contributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((50m, "Paystack-Recurring", "Successful", "r1", Inst), (c.Amount, c.PaymentMethod, c.Status, c.RecurringContributionId, c.InstitutionId));
        Assert.Equal((50m, 0m, 50m, 1m), (c.NetAmountToInstitution, c.PlatformFeeAmount, c.GrossChargeAmount, c.GatewayFeeAmount));
        Assert.Equal(0m, c.PlatformRevenueAmount);   // no subaccount, no split, so no platform revenue (and never a negative one)

        var campaign = await db.Campaigns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((50m, 1), (campaign.CollectedAmount, campaign.PaidCount));

        var gift = await db.RecurringContributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Successful", 0, "Active"), (gift.LastChargeStatus, gift.FailedAttemptCount, gift.Status));
        Assert.InRange((gift.NextChargeDate - DateTime.UtcNow).TotalDays, 27, 32);
        Assert.Contains(rig.Log.Entries, e => e.Message.Contains("ContributionConfirmed"));
    }

    [WorkflowFact]
    public async Task The_charge_request_carries_the_authorisation_member_email_unique_reference_and_metadata()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(), Gift());
        Charge(rig, true);
        ChargeAuthorizationRequest? sent = null;
        rig.Paystack.Setup(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()))
            .Callback<ChargeAuthorizationRequest>(r => sent = r)
            .ReturnsAsync(new ChargeAuthorizationResponse { Status = true, Data = new VerifyPaymentData { Status = "success", Amount = 5000 } });

        await Run(rig);

        Assert.NotNull(sent);
        Assert.Equal(("AUTH_1", "ama@x.com", 5000L), (sent!.AuthorizationCode, sent.Email, sent.Amount));
        Assert.Equal(32, sent.Reference.Length);
        Assert.Equal(("m1", "c1", "r1"), (sent.Metadata!["memberId"], sent.Metadata["campaignId"], sent.Metadata["recurringContributionId"]));
        Assert.Null(sent.Subaccount);
        Assert.Null(sent.TransactionCharge);
    }

    [WorkflowFact]
    public async Task With_a_subaccount_the_payer_covers_the_fees_and_the_split_is_sent()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(i => { i.PaystackSubaccountCode = "ACCT_1"; i.PlatformFeePercentage = 2m; }), Camp(), Person(), Gift());
        ChargeAuthorizationRequest? sent = null;
        rig.Paystack.Setup(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()))
            .Callback<ChargeAuthorizationRequest>(r => sent = r)
            .ReturnsAsync(new ChargeAuthorizationResponse { Status = true, Data = new VerifyPaymentData { Status = "success", Amount = 5200, Fees = 120 } });

        await Run(rig);

        var expected = PaystackFeeCalculator.CalculateZeroDeductionCharge(5000, 2m, 1.95m, 0, null, 2);
        Assert.Equal((expected.ChargeAmountSubunit, expected.TransactionChargeSubunit, "account", "ACCT_1"), (sent!.Amount, sent.TransactionCharge, sent.Bearer, sent.Subaccount));
        using var db = rig.Db();
        var c = await db.Contributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(50m, c.Amount);   // the institution still nets the full gift
        Assert.Equal(expected.TransactionChargeSubunit / 100m - 1.20m, c.PlatformRevenueAmount);
    }

    [WorkflowFact]
    public async Task A_single_batch_campaign_charges_into_that_batchs_approved_account()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(i => { i.PaystackSubaccountCode = "ACCT_INST"; }), Camp(years: [2018]), Person(), Gift(),
            new Batch { Id = "b", InstitutionId = Inst, Year = 2018, PayoutStatus = "Approved", UseInstitutionAccount = false, PaystackSubaccountCode = "ACCT_BATCH" });
        ChargeAuthorizationRequest? sent = null;
        rig.Paystack.Setup(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()))
            .Callback<ChargeAuthorizationRequest>(r => sent = r)
            .ReturnsAsync(new ChargeAuthorizationResponse { Status = true, Data = new VerifyPaymentData { Status = "success", Amount = 5200 } });

        await Run(rig);

        Assert.Equal("ACCT_BATCH", sent!.Subaccount);
    }

    [WorkflowFact]
    public async Task A_gift_that_is_not_yet_due_or_not_active_is_left_alone()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(),
            Gift(g => { g.Id = "future"; g.NextChargeDate = DateTime.UtcNow.AddDays(5); }),
            Gift(g => { g.Id = "paused"; g.Status = "Paused"; }),
            Gift(g => { g.Id = "cancelled"; g.Status = "Cancelled"; }));
        Charge(rig, true);

        await Run(rig);

        rig.Paystack.Verify(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()), Times.Never);
        using var db = rig.Db();
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task An_institution_with_recurring_giving_disabled_is_skipped_entirely()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(i => i.DisabledFeatures = [InstitutionFeatures.RecurringGiving]), Camp(), Person(), Gift());
        Charge(rig, true);

        await Run(rig);

        rig.Paystack.Verify(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()), Times.Never);
    }

    [WorkflowFact]
    public async Task Inactive_institutions_are_not_processed()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(i => i.Status = "Suspended"), Camp(), Person(), Gift());
        Charge(rig, true);

        await Run(rig);

        rig.Paystack.Verify(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()), Times.Never);
    }

    [WorkflowTheory]
    [InlineData(CampaignStatus.Closed)]
    [InlineData(CampaignStatus.Completed)]
    [InlineData(CampaignStatus.Archived)]
    public async Task A_gift_to_a_campaign_that_is_no_longer_active_is_paused_not_charged(CampaignStatus status)
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(status), Person(), Gift());
        Charge(rig, true);

        await Run(rig);

        rig.Paystack.Verify(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()), Times.Never);
        using var db = rig.Db();
        Assert.Equal("Paused", (await db.RecurringContributions.IgnoreQueryFilters().SingleAsync()).Status);
    }

    [WorkflowFact]
    public async Task A_gift_whose_campaign_was_deleted_is_paused()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Person(), Gift());

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal("Paused", (await db.RecurringContributions.IgnoreQueryFilters().SingleAsync()).Status);
    }

    [WorkflowFact]
    public async Task A_gift_with_no_email_anywhere_is_paused_without_being_charged()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(email: ""), Gift(g => g.Member = new MemberSnapshot { Id = "m1", Email = "" }));
        Charge(rig, true);

        await Run(rig);

        rig.Paystack.Verify(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()), Times.Never);
        using var db = rig.Db();
        Assert.Equal("Paused", (await db.RecurringContributions.IgnoreQueryFilters().SingleAsync()).Status);
    }

    [WorkflowFact]
    public async Task The_snapshot_email_is_used_when_the_member_record_is_gone()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Gift());   // no member row
        ChargeAuthorizationRequest? sent = null;
        rig.Paystack.Setup(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()))
            .Callback<ChargeAuthorizationRequest>(r => sent = r)
            .ReturnsAsync(new ChargeAuthorizationResponse { Status = true, Data = new VerifyPaymentData { Status = "success", Amount = 5000 } });

        await Run(rig);

        Assert.Equal("ama@x.com", sent!.Email);
    }

    [WorkflowFact]
    public async Task The_first_failure_keeps_the_gift_active_and_retries_in_three_days()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(), Gift());
        Charge(rig, false, status: "failed", gatewayResponse: "Insufficient funds");

        await Run(rig);

        using var db = rig.Db();
        var gift = await db.RecurringContributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Active", 1, "Failed: Insufficient funds"), (gift.Status, gift.FailedAttemptCount, gift.LastChargeStatus));
        Assert.InRange((gift.NextChargeDate - DateTime.UtcNow).TotalDays, 2.9, 3.1);
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, (await db.Campaigns.IgnoreQueryFilters().SingleAsync()).PaidCount);
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    [WorkflowFact]
    public async Task The_third_consecutive_failure_stops_the_gift_and_tells_the_member()
    {
        var rig = new ScheduledJobsRig();
        var due = DateTime.UtcNow.AddMinutes(-5);
        await rig.Seed(Institution(), Camp(), Person(), Gift(g => { g.FailedAttemptCount = 2; g.NextChargeDate = due; }));
        Charge(rig, false, status: "failed", gatewayResponse: "Card expired");

        await Run(rig);

        using var db = rig.Db();
        var gift = await db.RecurringContributions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Failed", 3, "Failed: Card expired"), (gift.Status, gift.FailedAttemptCount, gift.LastChargeStatus));
        Assert.Equal(due, gift.NextChargeDate, TimeSpan.FromSeconds(1));   // left as it was once stopped
        var note = await db.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("m1", "Recurring Gift Stopped", "r1"), (note.RecipientId, note.Title, note.RelatedEntityId));
        Assert.Contains("\"Library\"", note.Body);
        Assert.Contains("3 attempts", note.Body);
    }

    [WorkflowFact]
    public async Task A_success_reply_with_a_non_success_charge_status_counts_as_a_failure()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(), Gift());
        Charge(rig, true, status: "abandoned");

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(1, (await db.RecurringContributions.IgnoreQueryFilters().SingleAsync()).FailedAttemptCount);
        Assert.Equal(0, await db.Contributions.IgnoreQueryFilters().CountAsync());
    }

    [WorkflowFact]
    public async Task A_success_resets_the_failure_count()
    {
        var rig = new ScheduledJobsRig();
        await rig.Seed(Institution(), Camp(), Person(), Gift(g => g.FailedAttemptCount = 2));
        Charge(rig, true);

        await Run(rig);

        using var db = rig.Db();
        Assert.Equal(0, (await db.RecurringContributions.IgnoreQueryFilters().SingleAsync()).FailedAttemptCount);
    }

    [WorkflowFact]
    public async Task One_gift_failing_does_not_stop_the_others_in_the_same_cycle()
    {
        var rig = new ScheduledJobsRig();
        var second = Person(); second.Id = "m2"; second.Email = "kofi@x.com";
        await rig.Seed(Institution(), Camp(), Person(), second,
            Gift(g => { g.Id = "bad"; g.MemberId = "m1"; g.AuthorizationCode = "AUTH_BAD"; }),
            Gift(g => { g.Id = "good"; g.MemberId = "m2"; g.AuthorizationCode = "AUTH_GOOD"; g.Member = new MemberSnapshot { Id = "m2", Email = "kofi@x.com" }; }));
        rig.Paystack.Setup(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>())).ReturnsAsync((ChargeAuthorizationRequest r) =>
            new ChargeAuthorizationResponse
            {
                Status = r.AuthorizationCode == "AUTH_GOOD", Message = "declined",
                Data = new VerifyPaymentData { Status = r.AuthorizationCode == "AUTH_GOOD" ? "success" : "failed", Amount = 5000 },
            });

        await Run(rig);

        using var db = rig.Db();
        var gifts = await db.RecurringContributions.IgnoreQueryFilters().ToDictionaryAsync(g => g.Id);
        Assert.Equal(1, gifts["bad"].FailedAttemptCount);
        Assert.Equal(0, gifts["good"].FailedAttemptCount);
        Assert.Equal("m2", (await db.Contributions.IgnoreQueryFilters().SingleAsync()).MemberId);
    }

    [WorkflowFact]
    public async Task Each_institution_is_processed_independently()
    {
        var rig = new ScheduledJobsRig();
        var other = new Institution { Id = "i2", Name = "Other", Slug = "other", Status = "Active" };
        var otherCampaign = Camp(); otherCampaign.Id = "c2"; otherCampaign.InstitutionId = "i2";
        var otherPerson = Person(); otherPerson.Id = "m9"; otherPerson.InstitutionId = "i2"; otherPerson.Email = "z@x.com";
        await rig.Seed(Institution(), Camp(), Person(), Gift(), other, otherCampaign, otherPerson,
            Gift(g => { g.Id = "r2"; g.InstitutionId = "i2"; g.MemberId = "m9"; g.CampaignId = "c2"; g.Member = new MemberSnapshot { Id = "m9", Email = "z@x.com" }; }));
        Charge(rig, true);

        await Run(rig);

        using var db = rig.Db();
        var contributions = await db.Contributions.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(new[] { Inst, "i2" }, contributions.Select(c => c.InstitutionId).OrderByDescending(x => x == Inst));
        Assert.Equal(2, contributions.Count);
    }

    [WorkflowFact]
    public async Task With_no_institutions_the_run_completes_quietly()
    {
        var rig = new ScheduledJobsRig();
        await Run(rig);
        rig.Paystack.Verify(p => p.ChargeAuthorizationAsync(It.IsAny<ChargeAuthorizationRequest>()), Times.Never);
    }
}

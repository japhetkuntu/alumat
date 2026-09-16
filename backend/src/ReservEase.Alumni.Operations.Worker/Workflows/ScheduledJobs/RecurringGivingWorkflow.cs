using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Temporalio.Workflows;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Fired on a Temporal Schedule — the direct replacement for Member.Api's in-process
/// RecurringGivingSchedulerService/RecurringGivingProcessor, which used to make live
/// Paystack charge calls inside the web API process with no retry/idempotency semantics.
/// Fully decomposed to match ProcessContributionCallbackWorkflow's house style: this
/// workflow owns every branch (campaign-inactive pause, no-email pause, charge
/// success/failure, dunning/stop-after-3), activities are pure I/O.
/// </summary>
[Workflow("RecurringGivingDispatch")]
public class RecurringGivingWorkflow
{
    private const int MaxFailedAttempts = 3;

    [WorkflowRun]
    public async Task RunAsync()
    {
        var institutionIds = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveActiveInstitutionIdsAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        Workflow.Logger.LogInformation("Recurring giving dispatch starting for {Count} institutions", institutionIds.Count);

        var chargedCount = 0;
        foreach (var institutionId in institutionIds)
        {
            try
            {
                chargedCount += await ProcessInstitutionAsync(institutionId);
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Recurring giving dispatch failed for institution {InstitutionId}", institutionId);
            }
        }

        Workflow.Logger.LogInformation("Recurring giving dispatch complete: {Charged} charged successfully", chargedCount);
    }

    private async Task<int> ProcessInstitutionAsync(string institutionId)
    {
        var institution = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.LoadInstitutionForRecurringGivingAsync(institutionId),
            ScheduledJobsActivityOptions.DatabaseRead);
        if (institution is null) return 0;

        var due = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ResolveDueRecurringGiftsAsync(institutionId),
            ScheduledJobsActivityOptions.DatabaseRead);
        if (due.Count == 0) return 0;

        var chargedCount = 0;
        foreach (var gift in due)
        {
            try
            {
                if (await ChargeOneAsync(gift, institution))
                    chargedCount++;
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Unhandled error charging recurring gift {RecurringId}", gift.Id);
            }
        }

        Workflow.Logger.LogInformation(
            "Recurring giving cycle for institution {InstitutionId}: {Due} due, {Charged} charged successfully",
            institutionId, due.Count, chargedCount);

        return chargedCount;
    }

    private async Task<bool> ChargeOneAsync(RecurringGiftDue gift, RecurringInstitutionInfo institution)
    {
        var campaign = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.LoadCampaignForRecurringAsync(gift.CampaignId),
            ScheduledJobsActivityOptions.DatabaseRead);
        if (campaign is null || !campaign.IsActive)
        {
            await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.PauseRecurringGivingAsync(gift.Id, "campaign is no longer active"),
                ScheduledJobsActivityOptions.DatabaseWrite);
            return false;
        }

        var now = Workflow.UtcNow;
        var charge = await BuildZeroDeductionChargeAsync(gift.Amount, institution, campaign);
        var reference = Workflow.NewGuid().ToString("N");

        var response = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.ChargeRecurringGivingAsync(new ChargeAuthorizationRequest
            {
                AuthorizationCode = gift.AuthorizationCode,
                Email = gift.MemberEmail!,
                Amount = charge.amountSubunit,
                Reference = reference,
                Subaccount = charge.subaccount,
                TransactionCharge = charge.transactionCharge,
                Bearer = charge.bearer,
                Metadata = new Dictionary<string, string>
                {
                    { "memberId", gift.MemberId },
                    { "campaignId", gift.CampaignId },
                    { "recurringContributionId", gift.Id },
                },
            }),
            ScheduledJobsActivityOptions.ExternalGateway);

        var succeeded = response.Status && string.Equals(response.Data?.Status, "success", StringComparison.OrdinalIgnoreCase);

        if (succeeded)
        {
            var actualFee = response.Data?.Fees.HasValue == true ? response.Data.Fees.Value / 100m : charge.gatewayFee;
            var contribution = new Contribution
            {
                InstitutionId = campaign.InstitutionId,
                MemberId = gift.MemberId,
                Member = gift.Member,
                CampaignId = gift.CampaignId,
                Campaign = gift.Campaign ?? new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                Amount = gift.Amount,
                PaymentMethod = "Paystack-Recurring",
                TransactionRef = reference,
                Status = "Successful",
                ConfirmedAt = now,
                ConfirmedBy = "Paystack",
                CreatedBy = "system",
                PlatformFeeAmount = 0m,
                NetAmountToInstitution = gift.Amount,
                PlatformRevenueAmount = charge.transactionChargeAmount - actualFee,
                GatewayFeeAmount = actualFee,
                GrossChargeAmount = (response.Data?.Amount ?? charge.amountSubunit) / 100m,
                RecurringContributionId = gift.Id,
            };

            contribution = await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.CreateContributionFromRecurringAsync(contribution),
                ScheduledJobsActivityOptions.DatabaseWrite);

            await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.UpdateCampaignTotalsForRecurringAsync(campaign.Id, contribution.Amount),
                ScheduledJobsActivityOptions.DatabaseWrite);

            await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.MarkRecurringChargeSuccessfulAsync(gift.Id, now, now.AddMonths(1)),
                ScheduledJobsActivityOptions.DatabaseWrite);

            var confirmedMemberEmail = gift.MemberEmail ?? string.Empty;
            var confirmedMemberFirstName = gift.MemberFirstName ?? string.Empty;
            await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.EnqueueContributionConfirmedNotificationAsync(
                    NotificationRequest.ContributionConfirmed(
                        campaign.InstitutionId, gift.MemberId, confirmedMemberEmail, confirmedMemberFirstName,
                        contribution.Amount, campaign.Title, contribution.Id)),
                ScheduledJobsActivityOptions.DatabaseWrite);

            Workflow.Logger.LogInformation("Charged recurring gift {RecurringId}: {Amount} to campaign {CampaignId}", gift.Id, contribution.Amount, campaign.Id);
            return true;
        }

        var failureReason = response.Data?.GatewayResponse ?? response.Message;
        var newAttemptCount = gift.FailedAttemptCount + 1;
        var stopped = newAttemptCount >= MaxFailedAttempts;

        await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.MarkRecurringChargeFailedAsync(
                gift.Id, now, failureReason ?? "unknown error",
                stopped ? "Failed" : "Active",
                newAttemptCount,
                // Short dunning retry rather than waiting a full month for the next attempt.
                stopped ? null : now.AddDays(3)),
            ScheduledJobsActivityOptions.DatabaseWrite);

        if (stopped)
        {
            await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.CreateRecurringGivingStoppedNotificationAsync(campaign.InstitutionId, gift.MemberId, campaign.Title, gift.Id),
                ScheduledJobsActivityOptions.DatabaseWrite);
            Workflow.Logger.LogWarning("Recurring gift {RecurringId} stopped after {Attempts} failed attempts", gift.Id, newAttemptCount);
        }

        return false;
    }

    /// <summary>Mirrors the retired RecurringGivingProcessor.BuildZeroDeductionCharge exactly
    /// — pure math plus one DB read (the batch-subaccount lookup) for the ResolveSubaccountAsync
    /// fallback, kept here since the workflow owns this branching per house style.</summary>
    private async Task<(long amountSubunit, long? transactionCharge, string? bearer, decimal gatewayFee, decimal transactionChargeAmount, string? subaccount)>
        BuildZeroDeductionChargeAsync(decimal schoolAmount, RecurringInstitutionInfo institution, RecurringCampaignInfo campaign)
    {
        string? subaccount = institution.PaystackSubaccountCode;
        if (campaign.YearGroups is { Count: 1 })
        {
            var batchSubaccount = await Workflow.ExecuteActivityAsync(
                (ScheduledJobsActivities a) => a.ResolveBatchSubaccountAsync(campaign.InstitutionId, campaign.YearGroups[0]),
                ScheduledJobsActivityOptions.DatabaseRead);
            if (!string.IsNullOrEmpty(batchSubaccount))
                subaccount = batchSubaccount;
        }

        var schoolAmountSubunit = (long)Math.Round(schoolAmount * 100m, MidpointRounding.AwayFromZero);

        if (string.IsNullOrEmpty(subaccount))
            return (schoolAmountSubunit, null, null, 0m, 0m, null);

        var gatewayConfig = await Workflow.ExecuteActivityAsync(
            (ScheduledJobsActivities a) => a.GetPaystackGatewayFeeConfigAsync(),
            ScheduledJobsActivityOptions.DatabaseRead);

        var charge = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            schoolAmountSubunit,
            institution.PlatformFeePercentage,
            gatewayConfig.GatewayFeePercentage,
            gatewayConfig.GatewayFixedFeeSubunit,
            gatewayConfig.GatewayFeeCapSubunit,
            gatewayConfig.GatewayFeeSafetyBufferSubunit,
            institution.PlatformFeeFlatThreshold.HasValue ? (long)Math.Round(institution.PlatformFeeFlatThreshold.Value * 100m, MidpointRounding.AwayFromZero) : null,
            institution.PlatformFeeFlatAmount.HasValue ? (long)Math.Round(institution.PlatformFeeFlatAmount.Value * 100m, MidpointRounding.AwayFromZero) : null);

        return (
            charge.ChargeAmountSubunit,
            charge.TransactionChargeSubunit,
            "account",
            charge.GatewayFeeSubunit / 100m,
            charge.TransactionChargeSubunit / 100m,
            subaccount);
    }
}

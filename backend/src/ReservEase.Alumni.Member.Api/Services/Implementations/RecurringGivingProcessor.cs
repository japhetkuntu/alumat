using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Member.Api.Actors;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Charges every due RecurringContribution for the current tenant via
/// Paystack's charge_authorization endpoint — the same Zero-Deduction split
/// math and per-batch subaccount routing as a one-off contribution
/// (ContributionService.BuildZeroDeductionCharge/ResolveSubaccountAsync),
/// duplicated here in miniature since those are private to ContributionService
/// and this runs from a background scheduler, not a request.
/// </summary>
public class RecurringGivingProcessor(
    IAlumniPgRepository<RecurringContribution> recurringRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<Notification> notifRepo,
    AlumniDbContext db,
    ICurrentTenantService currentTenant,
    IPaystackService paystackService,
    PaystackConfig paystackConfig,
    INotificationActor notificationActor,
    ILogger<RecurringGivingProcessor> logger) : IRecurringGivingProcessor
{
    private const int MaxFailedAttempts = 3;

    public async Task<int> ChargeDueRecurringGivingAsync()
    {
        if (string.IsNullOrEmpty(currentTenant.InstitutionId))
            return 0;

        var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        if (institution is null || institution.DisabledFeatures.Contains(InstitutionFeatures.RecurringGiving))
            return 0;

        var now = DateTime.UtcNow;
        var due = (await recurringRepo.GetAllAsync(r => r.Status == "Active" && r.NextChargeDate <= now)).ToList();
        if (due.Count == 0)
            return 0;

        var chargedCount = 0;

        foreach (var recurring in due)
        {
            try
            {
                // Atomically "claim" this row before calling Paystack — a
                // single UPDATE ... WHERE is a single round-trip Postgres
                // itself serializes, so exactly one caller can ever win it,
                // unlike the read-then-later-write this used to be (which
                // let two concurrent runs — a second Member.Api instance
                // under normal horizontal scaling, or this same scheduler
                // firing again after a crash mid-charge — both read the row
                // as "due", both charge the member's card, and only then
                // both try to write the result). The claim bumps
                // NextChargeDate forward by a short, bounded window rather
                // than the real one-month cycle; if charging fails partway
                // (e.g. this process crashes right after claiming), the row
                // becomes due again in a day instead of being stuck forever
                // or double-charged today. A genuinely successful/failed
                // charge overwrites this placeholder with its real
                // NextChargeDate immediately after, in ChargeOneAsync.
                var claimed = await db.Set<RecurringContribution>()
                    .Where(r => r.Id == recurring.Id && r.Status == "Active" && r.NextChargeDate <= now)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.NextChargeDate, now.AddDays(1)));
                if (claimed == 0)
                {
                    logger.LogInformation("Recurring gift {RecurringId} was already claimed by another run — skipping", recurring.Id);
                    continue;
                }

                if (await ChargeOneAsync(recurring, institution, now))
                    chargedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error charging recurring gift {RecurringId}", recurring.Id);
            }
        }

        logger.LogInformation(
            "Recurring giving cycle for institution {InstitutionId}: {Due} due, {Charged} charged successfully",
            institution?.Id, due.Count, chargedCount);

        return chargedCount;
    }

    private async Task<bool> ChargeOneAsync(RecurringContribution recurring, Institution? institution, DateTime now)
    {
        var campaign = await campaignRepo.GetByIdAsync(recurring.CampaignId);
        if (campaign is null || campaign.Status != CampaignStatus.Active)
        {
            recurring.Status = "Paused";
            recurring.UpdatedBy = "system";
            await recurringRepo.UpdateAsync(recurring);
            logger.LogInformation("Paused recurring gift {RecurringId} — campaign {CampaignId} is no longer active", recurring.Id, recurring.CampaignId);
            return false;
        }

        var member = await memberRepo.GetByIdAsync(recurring.MemberId);
        var email = member?.Email ?? recurring.Member?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            recurring.Status = "Paused";
            recurring.UpdatedBy = "system";
            await recurringRepo.UpdateAsync(recurring);
            return false;
        }

        var charge = BuildZeroDeductionCharge(recurring.Amount, institution);
        var subaccount = await ResolveSubaccountAsync(campaign, institution);
        var reference = Guid.NewGuid().ToString("N");

        var response = await paystackService.ChargeAuthorizationAsync(new ChargeAuthorizationRequest
        {
            AuthorizationCode = recurring.AuthorizationCode,
            Email = email,
            Amount = charge.amountSubunit,
            Reference = reference,
            Subaccount = subaccount,
            TransactionCharge = charge.transactionCharge,
            Bearer = charge.bearer,
            Metadata = new Dictionary<string, string>
            {
                { "memberId", recurring.MemberId },
                { "campaignId", recurring.CampaignId },
                { "recurringContributionId", recurring.Id },
            },
        });

        var succeeded = response.Status && string.Equals(response.Data?.Status, "success", StringComparison.OrdinalIgnoreCase);

        if (succeeded)
        {
            var contribution = new Contribution
            {
                InstitutionId = campaign.InstitutionId,
                MemberId = recurring.MemberId,
                Member = recurring.Member,
                CampaignId = recurring.CampaignId,
                Campaign = recurring.Campaign ?? new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                Amount = recurring.Amount,
                PaymentMethod = "Paystack-Recurring",
                TransactionRef = reference,
                Status = "Successful",
                ConfirmedAt = now,
                ConfirmedBy = "Paystack",
                CreatedBy = "system",
                PlatformFeeAmount = 0m,
                NetAmountToInstitution = recurring.Amount,
                PlatformRevenueAmount = charge.transactionChargeAmount - (response.Data?.Fees.HasValue == true ? response.Data.Fees.Value / 100m : charge.gatewayFee),
                GatewayFeeAmount = response.Data?.Fees.HasValue == true ? response.Data.Fees.Value / 100m : charge.gatewayFee,
                GrossChargeAmount = (response.Data?.Amount ?? charge.amountSubunit) / 100m,
                RecurringContributionId = recurring.Id,
            };
            await contributionRepo.AddAsync(contribution);

            campaign.CollectedAmount += contribution.Amount;
            campaign.PaidCount += 1;
            await campaignRepo.UpdateAsync(campaign);

            notificationActor.Tell(new DispatchContributionConfirmedCommand(
                campaign.InstitutionId, recurring.MemberId, email, recurring.Member?.FirstName ?? string.Empty,
                contribution.Amount, campaign.Title, contribution.Id));

            recurring.LastChargeAt = now;
            recurring.LastChargeStatus = "Successful";
            recurring.FailedAttemptCount = 0;
            recurring.NextChargeDate = now.AddMonths(1);
            recurring.UpdatedBy = "system";
            await recurringRepo.UpdateAsync(recurring);

            logger.LogInformation("Charged recurring gift {RecurringId}: {Amount} to campaign {CampaignId}", recurring.Id, contribution.Amount, campaign.Id);
            return true;
        }

        var failureReason = response.Data?.GatewayResponse ?? response.Message;
        recurring.LastChargeAt = now;
        recurring.LastChargeStatus = $"Failed: {failureReason}";
        recurring.FailedAttemptCount += 1;
        recurring.UpdatedBy = "system";

        if (recurring.FailedAttemptCount >= MaxFailedAttempts)
        {
            recurring.Status = "Failed";
            await notifRepo.AddAsync(new Notification
            {
                InstitutionId = campaign.InstitutionId,
                RecipientId = recurring.MemberId,
                RecipientType = "Member",
                Title = "Recurring Gift Stopped",
                Body = $"We couldn't charge your monthly gift to \"{campaign.Title}\" after {MaxFailedAttempts} attempts, so it's been stopped. Set up a new one anytime from your giving history.",
                Type = "ContributionRejected",
                RelatedEntityId = recurring.Id,
                RelatedEntityType = "RecurringContribution",
                CreatedBy = "system",
            });
            logger.LogWarning("Recurring gift {RecurringId} stopped after {Attempts} failed attempts", recurring.Id, recurring.FailedAttemptCount);
        }
        else
        {
            // Short dunning retry rather than waiting a full month for the next attempt.
            recurring.NextChargeDate = now.AddDays(3);
        }

        await recurringRepo.UpdateAsync(recurring);
        return false;
    }

    /// <summary>Mirrors ContributionService.BuildZeroDeductionCharge exactly — kept here since the original is private and this runs off-request.</summary>
    private (long amountSubunit, long? transactionCharge, string? bearer, decimal platformFee, decimal gatewayFee, decimal transactionChargeAmount, decimal grossCharge)
        BuildZeroDeductionCharge(decimal schoolAmount, Institution? institution)
    {
        var schoolAmountSubunit = (long)Math.Round(schoolAmount * 100m, MidpointRounding.AwayFromZero);

        if (institution is null || string.IsNullOrEmpty(institution.PaystackSubaccountCode))
            return (schoolAmountSubunit, null, null, 0m, 0m, 0m, schoolAmount);

        var charge = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            schoolAmountSubunit,
            institution.PlatformFeePercentage,
            paystackConfig.GatewayFeePercentage,
            paystackConfig.GatewayFixedFeeSubunit,
            paystackConfig.GatewayFeeCapSubunit,
            paystackConfig.GatewayFeeSafetyBufferSubunit,
            institution.PlatformFeeFlatThreshold.HasValue ? (long)Math.Round(institution.PlatformFeeFlatThreshold.Value * 100m, MidpointRounding.AwayFromZero) : null,
            institution.PlatformFeeFlatAmount.HasValue ? (long)Math.Round(institution.PlatformFeeFlatAmount.Value * 100m, MidpointRounding.AwayFromZero) : null);

        return (
            charge.ChargeAmountSubunit,
            charge.TransactionChargeSubunit,
            "account",
            charge.PlatformFeeSubunit / 100m,
            charge.GatewayFeeSubunit / 100m,
            charge.TransactionChargeSubunit / 100m,
            charge.ChargeAmountSubunit / 100m);
    }

    /// <summary>Mirrors ContributionService.ResolveSubaccountAsync exactly, for the same reason.</summary>
    private async Task<string?> ResolveSubaccountAsync(Campaign campaign, Institution? institution)
    {
        if (campaign.YearGroups is { Count: 1 } && !string.IsNullOrEmpty(currentTenant.InstitutionId))
        {
            var year = campaign.YearGroups[0];
            var batch = await db.Set<Batch>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.InstitutionId == currentTenant.InstitutionId && b.Year == year);
            if (batch is { PayoutStatus: "Approved", UseInstitutionAccount: false } && !string.IsNullOrEmpty(batch.PaystackSubaccountCode))
                return batch.PaystackSubaccountCode;
        }

        return institution?.PaystackSubaccountCode;
    }
}

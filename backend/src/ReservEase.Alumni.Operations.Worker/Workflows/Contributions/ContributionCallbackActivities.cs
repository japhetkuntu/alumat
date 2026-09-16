using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Activities;
using Temporalio.Exceptions;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Contributions;

/// <summary>
/// One activity, one I/O call — the workflow (ProcessContributionCallbackWorkflow)
/// owns the sequencing and every branch/decision (fee reconciliation math,
/// membership-active computation, member-number arithmetic, all pure). Registered
/// via AddScopedActivities, so each call gets its own DI scope automatically.
/// </summary>
public class ContributionCallbackActivities(
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<PaymentTransaction> paymentTransactionRepo,
    IAlumniPgRepository<RecurringContribution> recurringRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IPaystackService paystackService,
    IRedisService<MemberRedisConfig> redis,
    ITemporalClientProvider temporalProvider,
    ILogger<ContributionCallbackActivities> logger)
{
    [Activity("ContributionCallback.LoadTransaction")]
    public virtual Task<PaymentTransaction?> LoadTransactionAsync(string reference) =>
        Wrap(() => paymentTransactionRepo.GetOneAsync(t => t.Reference == reference, ignoreQueryFilters: true), "load payment transaction", reference);

    [Activity("ContributionCallback.LoadCampaign")]
    public virtual Task<Campaign?> LoadCampaignAsync(string campaignId) =>
        Wrap(() => campaignRepo.GetOneAsync(c => c.Id == campaignId, ignoreQueryFilters: true), "load campaign", campaignId);

    [Activity("ContributionCallback.LoadMember")]
    public virtual Task<MemberEntity?> LoadMemberAsync(string memberId) =>
        Wrap(() => memberRepo.GetOneAsync(m => m.Id == memberId, ignoreQueryFilters: true), "load member", memberId);

    [Activity("ContributionCallback.CreateTransaction")]
    public virtual Task<PaymentTransaction> CreateTransactionAsync(PaymentTransaction transaction) =>
        Wrap(async () => { await paymentTransactionRepo.AddAsync(transaction); return transaction; }, "create payment transaction", transaction.Reference);

    [Activity("ContributionCallback.SaveTransaction")]
    public virtual Task SaveTransactionAsync(PaymentTransaction transaction) =>
        Wrap(() => paymentTransactionRepo.UpdateAsync(transaction), "save payment transaction", transaction.Reference);

    [Activity("ContributionCallback.VerifyPaystackPayment")]
    public virtual Task<ContributionPaystackVerifyResult> VerifyPaystackPaymentAsync(string reference) =>
        Wrap(async () =>
        {
            var response = await paystackService.VerifyPaymentAsync(reference);
            return new ContributionPaystackVerifyResult
            {
                Status = response.Status,
                Message = response.Message,
                PaystackStatus = response.Data?.Status?.ToLowerInvariant() ?? "unknown",
                GrossAmount = (response.Data?.Amount ?? 0) / 100m,
                GatewayFee = response.Data?.Fees.HasValue == true ? response.Data!.Fees!.Value / 100m : (decimal?)null,
                GatewayResponse = response.Data?.GatewayResponse,
                Authorization = response.Data?.Authorization,
            };
        }, "verify Paystack payment", reference);

    [Activity("ContributionCallback.LoadContributionByReference")]
    public virtual Task<Contribution?> LoadContributionByReferenceAsync(string reference, string memberId) =>
        Wrap(() => contributionRepo.GetOneAsync(c => c.TransactionRef == reference && c.MemberId == memberId, ignoreQueryFilters: true), "load contribution by reference", reference);

    [Activity("ContributionCallback.LoadPriorSuccessfulMembershipContribution")]
    public virtual Task<Contribution?> LoadPriorSuccessfulMembershipContributionAsync(string campaignId, string memberId) =>
        Wrap(() => contributionRepo.GetOneAsync(c => c.CampaignId == campaignId && c.MemberId == memberId && c.Status == "Successful", ignoreQueryFilters: true), "load prior membership contribution", campaignId);

    [Activity("ContributionCallback.CreateContribution")]
    public virtual Task<Contribution> CreateContributionAsync(Contribution contribution) =>
        Wrap(async () => { await contributionRepo.AddAsync(contribution); return contribution; }, "create contribution", contribution.TransactionRef ?? contribution.Id);

    [Activity("ContributionCallback.DispatchContributionConfirmed")]
    public virtual Task DispatchContributionConfirmedAsync(string institutionId, string memberId, string memberEmail, string memberFirstName, decimal amount, string campaignTitle, string contributionId) =>
        Wrap(() => temporalProvider.EnqueueNotificationAsync(
            NotificationRequest.ContributionConfirmed(institutionId, memberId, memberEmail, memberFirstName, amount, campaignTitle, contributionId),
            logger), "dispatch contribution confirmed notification", contributionId);

    [Activity("ContributionCallback.LoadActiveRecurringGiving")]
    public virtual Task<RecurringContribution?> LoadActiveRecurringGivingAsync(string memberId, string campaignId) =>
        Wrap(() => recurringRepo.GetOneAsync(r => r.MemberId == memberId && r.CampaignId == campaignId && r.Status == "Active", ignoreQueryFilters: true), "load active recurring giving", memberId);

    [Activity("ContributionCallback.CreateRecurringGiving")]
    public virtual Task CreateRecurringGivingAsync(RecurringContribution recurring) =>
        Wrap(() => recurringRepo.AddAsync(recurring), "create recurring giving", recurring.MemberId);

    [Activity("ContributionCallback.UpdateCampaignTotals")]
    public virtual Task UpdateCampaignTotalsAsync(string campaignId, decimal amount) =>
        Wrap(async () =>
        {
            var campaign = await campaignRepo.GetOneAsync(c => c.Id == campaignId, ignoreQueryFilters: true);
            if (campaign is null) return;
            campaign.CollectedAmount += amount;
            campaign.PaidCount += 1;
            await campaignRepo.UpdateAsync(campaign);
        }, "update campaign totals", campaignId);

    /// <summary>Bundled read (three independent lookups, no branching between them) — the
    /// caller must already know the member's graduation year (load the member first via
    /// LoadMemberAsync) since it's a filter input here, not an output.</summary>
    [Activity("ContributionCallback.LoadMembershipReevalData")]
    public virtual Task<MembershipReevalData> LoadMembershipReevalDataAsync(string institutionId, string memberId, int? graduationYear, int currentYear) =>
        Wrap(async () =>
        {
            // Institution scoped explicitly since ignoreQueryFilters bypasses the usual tenant filter.
            var requiredCampaigns = await campaignRepo.GetQueryable(c => c.InstitutionId == institutionId
                    && c.IsMembershipCampaign && c.MembershipYear.HasValue
                    && c.MembershipYear.Value >= graduationYear
                    && c.MembershipYear.Value <= currentYear,
                ignoreQueryFilters: true).ToListAsync();

            var confirmedContributions = await contributionRepo.GetQueryable(
                c => c.InstitutionId == institutionId && c.MemberId == memberId && c.Status == "Successful",
                ignoreQueryFilters: true).ToListAsync();

            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);

            return new MembershipReevalData
            {
                RequiredCampaignIds = requiredCampaigns.Select(c => c.Id).ToList(),
                PaidCampaignIds = confirmedContributions.Select(c => c.CampaignId).Distinct().ToList(),
                MembershipActivePolicy = institution?.MemberActivePolicy,
            };
        }, "load membership reevaluation data", memberId);

    [Activity("ContributionCallback.GetNextMemberNumber")]
    public virtual Task<string> GetNextMemberNumberAsync(string institutionId, int? graduationYear) =>
        Wrap(async () =>
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            var slug = !string.IsNullOrWhiteSpace(institution?.Slug) ? institution.Slug.ToUpperInvariant() : "MEMBER";
            var prefix = $"{slug}-{graduationYear}-";

            var existingWithNumber = await memberRepo.GetQueryable(
                m => m.InstitutionId == institutionId && m.MemberNumber != null && m.MemberNumber.StartsWith(prefix),
                ignoreQueryFilters: true).ToListAsync();
            var maxSeq = existingWithNumber
                .Select(m => int.TryParse(m.MemberNumber![prefix.Length..], out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{(maxSeq + 1):D4}";
        }, "get next member number", institutionId);

    [Activity("ContributionCallback.UpdateMemberMembership")]
    public virtual Task UpdateMemberMembershipAsync(MemberEntity member) =>
        Wrap(() => memberRepo.UpdateAsync(member), "update member membership", member.Id);

    [Activity("ContributionCallback.ClearReferenceCache")]
    public virtual Task ClearReferenceCacheAsync(string reference) =>
        Wrap(() => redis.RemoveAsync($"paystack:ref:{reference}"), "clear reference cache", reference);

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }

    private static async Task Wrap(Func<Task> action, string what, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }
}

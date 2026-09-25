using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Extensions;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;

public class ContributionService : IContributionService
{
    private readonly IAlumniPgRepository<Contribution> contributionRepo;
    private readonly IAlumniPgRepository<Campaign> campaignRepo;
    private readonly IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member> memberRepo;
    private readonly IAlumniPgRepository<PaymentTransaction> paymentTransactionRepo;
    private readonly IAlumniPgRepository<Institution> institutionRepo;
    private readonly IAlumniPgRepository<PostgresDb.Sdk.Entities.Alumni.Batch> batchRepo;
    private readonly IAlumniPgRepository<RecurringContribution> recurringRepo;
    private readonly IAlumniPgRepository<PlatformSettings> platformSettingsRepo;
    private readonly ICurrentTenantService currentTenant;
    private readonly IPaystackService paystackService;
    private readonly PaystackConfig paystackConfig;
    private readonly IRedisService<MemberRedisConfig> redis;
    private readonly ITemporalClientProvider temporalProvider;
    private readonly ILogger<ContributionService> logger;
    private readonly string _paystackCallbackUrl;

    /// <summary>
    /// Member numbers are prefixed by the current institution's own slug
    /// (e.g. "GREENFIELD-2026-0001"), not a fixed string — this platform
    /// hosts any number of institutions, so the prefix has to identify
    /// which one a member number belongs to.
    /// </summary>
    private async Task<string> GetMemberNumberPrefixAsync(int? graduationYear)
        => await GetMemberNumberPrefixForInstitutionAsync(currentTenant.InstitutionId, graduationYear);

    /// <summary>
    /// Tenant-agnostic variant for code paths (like the Paystack webhook) that don't
    /// have a reliable ambient tenant — the institution must be passed explicitly,
    /// resolved from the data (e.g. the campaign the payment belongs to).
    /// </summary>
    private async Task<string> GetMemberNumberPrefixForInstitutionAsync(string? institutionId, int? graduationYear)
    {
        string slug = "MEMBER";
        if (!string.IsNullOrEmpty(institutionId))
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (!string.IsNullOrWhiteSpace(institution?.Slug))
                slug = institution.Slug.ToUpperInvariant();
        }
        return $"{slug}-{graduationYear}-";
    }

    /// <summary>
    /// A campaign targeting exactly one batch (YearGroups with a single
    /// entry) settles into that batch's own Paystack subaccount instead of
    /// the institution's, once the batch's payout setup has been approved —
    /// see BatchesController.SubmitPayoutSetup / Platform.Api's
    /// BatchPayoutsController. A campaign with no year groups, more than one,
    /// or a community-only audience always falls back to the institution's
    /// account (unambiguous only for a single-batch audience). The platform
    /// fee percentage is always the institution's — batches never have their
    /// own.
    /// </summary>
    private async Task<string?> ResolveSubaccountAsync(Campaign? campaign, Institution? institution)
    {
        if (campaign?.YearGroups is { Count: 1 } && !string.IsNullOrEmpty(currentTenant.InstitutionId))
        {
            var year = campaign.YearGroups[0];
            var batch = await batchRepo.GetOneAsync(b => b.InstitutionId == currentTenant.InstitutionId && b.Year == year);
            if (batch is { PayoutStatus: "Approved", UseInstitutionAccount: false } && !string.IsNullOrEmpty(batch.PaystackSubaccountCode))
                return batch.PaystackSubaccountCode;
        }

        return institution?.PaystackSubaccountCode;
    }

    private async Task<Institution?> GetCurrentInstitutionAsync() =>
        string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);

    /// <summary>
    /// Zero-Deduction pricing: builds the InitializePaymentRequest fields so the
    /// institution nets 100% of schoolAmount and our platform fee is collected
    /// from the payer's grossed-up charge — never deducted from the institution.
    /// Falls back to a plain, unsplit charge when the institution has no Paystack
    /// subaccount configured yet (onboarding not complete), preserving today's
    /// behavior for those institutions rather than blocking payment.
    /// </summary>
    private (long amountSubunit, long? transactionCharge, string? bearer, decimal platformFee, decimal gatewayFee, decimal transactionChargeAmount, decimal grossCharge)
        BuildZeroDeductionCharge(decimal schoolAmount, Institution? institution)
    {
        var schoolAmountSubunit = (long)Math.Round(schoolAmount * 100m, MidpointRounding.AwayFromZero);

        if (institution is null || string.IsNullOrEmpty(institution.PaystackSubaccountCode))
        {
            return (schoolAmountSubunit, null, null, 0m, 0m, 0m, schoolAmount);
        }

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
            "account", // Paystack's fee must come from our share, never the institution's.
            charge.PlatformFeeSubunit / 100m,
            charge.GatewayFeeSubunit / 100m,
            charge.TransactionChargeSubunit / 100m,
            charge.ChargeAmountSubunit / 100m);
    }

    public async Task<IApiResponse<PgPagedResult<ContributionDto>>> GetMyContributionsAsync(
        string memberId, ContributionFilter filter)
    {
        try
        {
            logger.LogInformation("GetMyContributions for member {MemberId} with filter: {Filter}", memberId, filter.Serialize());
            var result = await contributionRepo.GetPagedAsync(
                filter.Page, filter.PageSize, "CreatedAt", "desc",
                c => c.MemberId == memberId
                  && (string.IsNullOrEmpty(filter.CampaignId) || c.CampaignId == filter.CampaignId));

            // Backfill missing snapshots for contributions stored before jsonb columns were added.
            var needsBackfill = result.Results.Where(c => c.Member is null || c.Campaign is null).ToList();
            if (needsBackfill.Count > 0)
            {
                var memberIds = needsBackfill.Where(c => c.Member is null).Select(c => c.MemberId).Distinct().ToList();
                var campaignIds = needsBackfill.Where(c => c.Campaign is null).Select(c => c.CampaignId).Distinct().ToList();

                var members = memberIds.Count > 0
                    ? (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id)
                    : new Dictionary<string, ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>();
                var campaigns = campaignIds.Count > 0
                    ? (await campaignRepo.GetAllAsync(c => campaignIds.Contains(c.Id))).ToDictionary(c => c.Id)
                    : new Dictionary<string, Campaign>();

                foreach (var c in needsBackfill)
                {
                    if (c.Member is null && members.TryGetValue(c.MemberId, out var m))
                        c.Member = new MemberSnapshot { Id = m.Id, FirstName = m.FirstName, LastName = m.LastName, Email = m.Email, ProfilePictureUrl = m.ProfilePictureUrl };
                    if (c.Campaign is null && campaigns.TryGetValue(c.CampaignId, out var cam))
                        c.Campaign = new CampaignSnapshot { Id = cam.Id, Title = cam.Title };
                }

                await contributionRepo.UpdateRangeAsync(needsBackfill);
            }

            var dtoResult = new PgPagedResult<ContributionDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(c => c.ToDto()).ToList(),
            };

            // Backfill above only fills in rows that never got a snapshot — this
            // refreshes name/photo too, from the live Member record. Always the
            // same one member (the caller), so a single lookup, not a batch.
            var callerMember = await memberRepo.GetByIdAsync(memberId);
            if (callerMember is not null)
            {
                foreach (var dto in dtoResult.Results)
                {
                    dto.MemberName = $"{callerMember.FirstName} {callerMember.LastName}";
                    dto.MemberProfilePictureUrl = callerMember.ProfilePictureUrl;
                }
            }

            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving contributions for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ContributionDto>>("Failed to retrieve contributions");
        }
    }

    public async Task<IApiResponse<MyContributionSummaryDto>> GetMyContributionSummaryAsync(string memberId)
    {
        try
        {
            // Only the three columns the totals need — the full row carries jsonb snapshots.
            var rows = await contributionRepo
                .GetQueryable(c => c.MemberId == memberId && c.Status == "Successful")
                .Select(c => new { c.Amount, c.CampaignId, c.CreatedAt })
                .ToListAsync();

            var year = DateTime.UtcNow.Year;
            var summary = new MyContributionSummaryDto(
                rows.Sum(r => r.Amount),
                rows.Where(r => r.CreatedAt.Year == year).Sum(r => r.Amount),
                rows.Select(r => r.CampaignId).Distinct().ToList());

            return summary.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error summarising contributions for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<MyContributionSummaryDto>("Failed to retrieve contribution summary");
        }
    }

    public ContributionService(
        IAlumniPgRepository<Contribution> contributionRepo,
        IAlumniPgRepository<Campaign> campaignRepo,
        IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member> memberRepo,
        IAlumniPgRepository<PaymentTransaction> paymentTransactionRepo,
        IAlumniPgRepository<Institution> institutionRepo,
        IAlumniPgRepository<PostgresDb.Sdk.Entities.Alumni.Batch> batchRepo,
        IAlumniPgRepository<RecurringContribution> recurringRepo,
        IAlumniPgRepository<PlatformSettings> platformSettingsRepo,
        ICurrentTenantService currentTenant,
        IPaystackService paystackService,
        PaystackConfig paystackConfig,
        IRedisService<MemberRedisConfig> redis,
        ITemporalClientProvider temporalProvider,
        IConfiguration configuration,
        ILogger<ContributionService> logger)
    {
        this.contributionRepo = contributionRepo;
        this.campaignRepo = campaignRepo;
        this.memberRepo = memberRepo;
        this.paymentTransactionRepo = paymentTransactionRepo;
        this.institutionRepo = institutionRepo;
        this.batchRepo = batchRepo;
        this.recurringRepo = recurringRepo;
        this.platformSettingsRepo = platformSettingsRepo;
        this.currentTenant = currentTenant;
        this.paystackService = paystackService;
        this.paystackConfig = paystackConfig;
        this.redis = redis;
        this.temporalProvider = temporalProvider;
        this.logger = logger;

        // Ensure callback goes to our callback route so the app can show status modal.
        var callbackUrl = configuration["PaystackConfig:CallbackUrl"] ?? string.Empty;
        if (!string.IsNullOrEmpty(callbackUrl) && !callbackUrl.EndsWith("/callback", StringComparison.OrdinalIgnoreCase))
        {
            callbackUrl = callbackUrl.TrimEnd('/') + "/callback";
        }
        _paystackCallbackUrl = callbackUrl;
    }

    public async Task<IApiResponse<object>> InitiatePaystackPaymentAsync(
        InitiatePaystackPaymentRequest request, AuthData? member)
    {
        try
        {
            var isGuestPayment = member is null;
            var memberId = member?.Id ?? string.Empty;
            var memberEmail = member?.Email ?? request.Email;
            string? sharedByMemberId = null;

            // If a guest request includes an email, try to link to a pending member's account
            if (string.IsNullOrEmpty(memberId) && !string.IsNullOrWhiteSpace(memberEmail))
            {
                var emailLower = memberEmail.ToLower().Trim();
                var pendingMember = await memberRepo.GetOneAsync(m => m.Email == emailLower && m.Status == "Pending");
                if (pendingMember is not null)
                {
                    memberId = pendingMember.Id;
                    member = new AuthData
                    {
                        Id = pendingMember.Id,
                        Email = pendingMember.Email,
                        FirstName = pendingMember.FirstName,
                        LastName = pendingMember.LastName,
                        ProfilePictureUrl = pendingMember.ProfilePictureUrl,
                    };
                    logger.LogInformation("Guest payment linked to pending member {MemberId} for email {Email}", memberId, memberEmail);
                }
            }

            // Still not identified — if this campaign was reached through a shared link,
            // attribute the payment to whoever shared it (marked as a guest payment below),
            // rather than leaving it orphaned with no member at all.
            if (string.IsNullOrEmpty(memberId) && !string.IsNullOrWhiteSpace(request.SharedByMemberId))
            {
                var sharer = await memberRepo.GetByIdAsync(request.SharedByMemberId);
                if (sharer is not null)
                {
                    memberId = sharer.Id;
                    sharedByMemberId = sharer.Id;
                    member = new AuthData
                    {
                        Id = sharer.Id,
                        Email = sharer.Email,
                        FirstName = sharer.FirstName,
                        LastName = sharer.LastName,
                        ProfilePictureUrl = sharer.ProfilePictureUrl,
                    };
                    logger.LogInformation("Guest payment attributed to sharer {MemberId}, marked as guest payment", memberId);
                }
            }

            logger.LogInformation("InitiatePaystackPayment request: {Request} by member {MemberId}", request.Serialize(), string.IsNullOrEmpty(memberId) ? "anonymous" : memberId);

            if (string.IsNullOrWhiteSpace(memberEmail))
            {
                // Paystack requires an email, but UI no longer forces it. Use an anonymized
                // fallback under a real TLD — Paystack's validator rejects the RFC-2606
                // reserved ".invalid" TLD used here previously as a malformed address.
                memberEmail = $"guest+{Guid.NewGuid():N}@guest.alumunion.com";
            }

            var campaign = await campaignRepo.GetByIdAsync(request.CampaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Campaign not found");

            // For membership campaigns, transparently route through the membership renewal flow
            if (campaign.IsMembershipCampaign)
            {
                if (member is null)
                    return ApiResponseExtensions.ToBadRequestApiResponse<object>("You must be logged in to pay for a membership campaign.");

                return await InitiateMembershipRenewalAsync(
                    new InitiateMembershipRenewalRequest(request.CampaignId, 1, "online", request.CallbackUrl), member);
            }

            if (request.Amount <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Amount must be greater than zero.");

            // Platform-wide toggle, off by default — deliberately never
            // applies to membership-dues campaigns (handled entirely by the
            // branch above, before this point is ever reached).
            if (campaign.Deadline < DateTime.UtcNow)
            {
                var platformSettings = await platformSettingsRepo.GetByIdAsync(PlatformSettings.SingletonId);
                if (platformSettings?.BlockOverdueCampaignPayments == true)
                    return ApiResponseExtensions.ToBadRequestApiResponse<object>("This campaign's deadline has passed — contributions are no longer being accepted.");
            }

            if (request.SetupRecurringGiving && member is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You must be logged in to set up a monthly gift.");

            var currentInstitution = await GetCurrentInstitutionAsync();

            if (request.SetupRecurringGiving && currentInstitution is { } inst && inst.DisabledFeatures.Contains(InstitutionFeatures.RecurringGiving))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Recurring giving is not enabled for this institution.");

            var charge = BuildZeroDeductionCharge(request.Amount, currentInstitution);
            var subaccount = await ResolveSubaccountAsync(campaign, currentInstitution);

            // The subaccount's own dashboard config always shows a static
            // 0%-platform/100%-institution split (deliberate — see
            // InstitutionManagementService's Zero-Deduction comments); the
            // real per-payment split only ever exists here, in what we
            // actually send Paystack. Logged so it's independently
            // verifiable from our own logs, not just trusted from code.
            logger.LogInformation(
                "Zero-Deduction charge for campaign {CampaignId}, institution {InstitutionId}: schoolAmount={SchoolAmount}, platformFee={PlatformFee}, gatewayFee={GatewayFee}, chargeAmount={ChargeAmount}, transactionCharge={TransactionCharge}, subaccount={Subaccount}, bearer={Bearer}",
                request.CampaignId, currentInstitution?.Id, request.Amount, charge.platformFee, charge.gatewayFee, charge.grossCharge, charge.transactionCharge, subaccount, charge.bearer);

            // Generate our reference and persist the Pending transaction BEFORE ever
            // telling Paystack about it. This guarantees any callback Paystack could
            // possibly send for this reference already has a matching row — the
            // callback workflow can then treat "reference not found" as a wrong/bogus
            // reference rather than a data-loss case it needs to recover from. If this
            // write fails, we never call Paystack at all, so no reference exists
            // anywhere that we don't already know about.
            var reference = PaystackReferencePrefix.NewReference(PaystackReferencePrefix.Contribution);
            var transaction = new PaymentTransaction
            {
                MemberId = memberId,
                Member = member is not null ? new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                } : null,
                CampaignId = campaign.Id,
                Campaign = new CampaignSnapshot
                {
                    Id = campaign.Id,
                    Title = campaign.Title,
                },
                Amount = request.Amount,
                Reference = reference,
                Status = "Pending",
                PaymentMethod = "Paystack",
                CreatedBy = string.IsNullOrEmpty(memberId) ? "anonymous" : memberId,
                IsGuestPayment = isGuestPayment,
                SharedByMemberId = sharedByMemberId,
                ShowOnWallOfSupport = request.ShowOnWallOfSupport,
                SetupRecurringGiving = request.SetupRecurringGiving,
                PlatformFeeAmount = charge.platformFee,
                GatewayFeeAmount = charge.gatewayFee,
                TransactionChargeAmount = charge.transactionChargeAmount,
                GrossChargeAmount = charge.grossCharge,
            };

            await paymentTransactionRepo.AddAsync(transaction);
            await redis.SetAsync($"paystack:ref:{reference}", new PaystackReferenceInfo { MemberId = memberId, CampaignId = request.CampaignId, IsGuestPayment = isGuestPayment, SharedByMemberId = sharedByMemberId, SetupRecurringGiving = request.SetupRecurringGiving }, TimeSpan.FromHours(24));

            var response = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Reference = reference,
                Email = memberEmail,
                Amount = charge.amountSubunit,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "memberId", memberId },
                    { "campaignId", request.CampaignId },
                },
                Subaccount = subaccount,
                TransactionCharge = charge.transactionCharge,
                Bearer = charge.bearer,
            });

            if (!response.Status)
            {
                // Paystack never accepted this reference, so it will never send a
                // callback for it either — mark it Failed rather than leaving a
                // phantom Pending row nothing will ever resolve.
                transaction.Status = "Failed";
                transaction.FailureMessage = response.Message;
                await paymentTransactionRepo.UpdateAsync(transaction);
                await redis.RemoveAsync($"paystack:ref:{reference}");
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(response.Message);
            }

            logger.LogInformation("Paystack payment initiated for member {MemberId}, campaign {CampaignId}", string.IsNullOrEmpty(memberId) ? "anonymous" : memberId, request.CampaignId);
            return ((object)new { authorizationUrl = response.Data?.AuthorizationUrl, reference })
                .ToOkApiResponse("Payment initiated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initiating Paystack payment for member {MemberId}", member?.Id ?? "anonymous");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to initiate payment");
        }
    }

    public async Task<IApiResponse<object>> InitiateMembershipRenewalAsync(InitiateMembershipRenewalRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("InitiateMembershipRenewal request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            if (request.Years <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Renewal years must be at least 1.");

            if (request.Years != 1)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Membership renewal is one-time per user and must be 1 year.");

            var campaign = await campaignRepo.GetByIdAsync(request.CampaignId);
            if (campaign is null || !campaign.IsMembershipCampaign)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Membership campaign not found");

            var alreadyPaid = await contributionRepo.GetOneAsync(c => c.CampaignId == campaign.Id && c.MemberId == member.Id && c.Status == "Successful");
            if (alreadyPaid is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You have already paid this membership campaign.");

            // Determine the correct amount based on the member's employment status
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);

            // Members should only pay for membership campaigns with a year >= their graduation year
            if (campaign.MembershipYear.HasValue && memberEntity is not null && campaign.MembershipYear.Value < memberEntity.GraduationYear)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("This membership campaign is for a year before your graduation. You are not eligible.");
            var isPensioner = memberEntity?.EmploymentStatus == "Pensioner";
            var amountPerYear = isPensioner && campaign.PensionerAmountPerMember.HasValue
                ? campaign.PensionerAmountPerMember.Value
                : campaign.AmountPerMember;

            if (request.PaymentMethod.Equals("manual", StringComparison.OrdinalIgnoreCase))
            {
                if (!campaign.AllowManualPayments)
                    return ApiResponseExtensions.ToBadRequestApiResponse<object>("Manual payments are not enabled for membership.");

                var contribution = new Contribution
                {
                    MemberId = member.Id,
                    Member = new MemberSnapshot
                    {
                        Id = member.Id,
                        FirstName = member.FirstName,
                        LastName = member.LastName,
                        Email = member.Email,
                        ProfilePictureUrl = member.ProfilePictureUrl,
                    },
                    CampaignId = campaign.Id,
                    Campaign = new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                    Amount = amountPerYear * request.Years,
                    PaymentMethod = "Manual",
                    Status = "Pending",
                    Notes = $"Membership renewal for {request.Years} year(s)",
                    CreatedBy = member.Id,
                };

                await contributionRepo.AddAsync(contribution);
                var payload = new
                {
                    message = "Manual membership payment request created. Please follow bank/mobile payment instructions.",
                    campaign.BankAccount,
                    campaign.MobileMoneyAccount,
                    contributionId = contribution.Id,
                };
                return ApiResponseExtensions.ToOkApiResponse<object>(payload);
            }

            if (!request.PaymentMethod.Equals("online", StringComparison.OrdinalIgnoreCase))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid payment method. Use 'online' or 'manual'.");

            var amount = amountPerYear * request.Years;
            var currentInstitution = await GetCurrentInstitutionAsync();
            var charge = BuildZeroDeductionCharge(amount, currentInstitution);
            var subaccount = await ResolveSubaccountAsync(campaign, currentInstitution);

            // See the matching log in InitiatePaystackPaymentAsync — the
            // subaccount's dashboard config always shows a static
            // 0%-platform/100%-institution split by design; this is the
            // actual per-payment split, independently verifiable here.
            logger.LogInformation(
                "Zero-Deduction charge for membership renewal, campaign {CampaignId}, institution {InstitutionId}: schoolAmount={SchoolAmount}, platformFee={PlatformFee}, gatewayFee={GatewayFee}, chargeAmount={ChargeAmount}, transactionCharge={TransactionCharge}, subaccount={Subaccount}, bearer={Bearer}",
                campaign.Id, currentInstitution?.Id, amount, charge.platformFee, charge.gatewayFee, charge.grossCharge, charge.transactionCharge, subaccount, charge.bearer);

            // Same ordering fix as InitiatePaystackPaymentAsync — create the Pending
            // transaction before Paystack ever knows about the reference.
            var reference = PaystackReferencePrefix.NewReference(PaystackReferencePrefix.Contribution);
            var transaction = new PaymentTransaction
            {
                MemberId = member.Id,
                Member = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                },
                CampaignId = campaign.Id,
                Campaign = new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                Amount = amount,
                Reference = reference,
                Status = "Pending",
                PaymentMethod = "Paystack",
                MembershipYears = request.Years,
                CreatedBy = member.Id,
                PlatformFeeAmount = charge.platformFee,
                GatewayFeeAmount = charge.gatewayFee,
                TransactionChargeAmount = charge.transactionChargeAmount,
                GrossChargeAmount = charge.grossCharge,
            };

            await paymentTransactionRepo.AddAsync(transaction);
            await redis.SetAsync($"paystack:ref:{reference}", new PaystackReferenceInfo { MemberId = member.Id, CampaignId = campaign.Id }, TimeSpan.FromHours(24));

            var response = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Reference = reference,
                Email = member.Email,
                Amount = charge.amountSubunit,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "memberId", member.Id },
                    { "campaignId", campaign.Id },
                    { "membershipYears", request.Years.ToString() }
                },
                Subaccount = subaccount,
                TransactionCharge = charge.transactionCharge,
                Bearer = charge.bearer,
            });

            if (!response.Status)
            {
                transaction.Status = "Failed";
                transaction.FailureMessage = response.Message;
                await paymentTransactionRepo.UpdateAsync(transaction);
                await redis.RemoveAsync($"paystack:ref:{reference}");
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(response.Message);
            }

            return ((object)new { authorizationUrl = response.Data?.AuthorizationUrl, reference, amount, years = request.Years })
                .ToOkApiResponse("Membership payment initiated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initiating membership renewal for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to initiate membership renewal");
        }
    }

    private class PaystackReferenceInfo
    {
        public string MemberId { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public bool IsGuestPayment { get; set; }
        public string? SharedByMemberId { get; set; }
        public bool SetupRecurringGiving { get; set; }
    }

    private async Task<PaystackReferenceInfo?> GetReferenceInfoAsync(string reference)
    {
        try
        {
            return await redis.GetAsync<PaystackReferenceInfo>($"paystack:ref:{reference}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<IApiResponse<object>> VerifyPaystackPaymentAsync(string reference, AuthData? member)
    {
        try
        {
            var memberId = member?.Id;
            logger.LogInformation("VerifyPaystackPayment for reference: {Reference}, member {MemberId}", reference, memberId ?? "anonymous");

            if (!temporalProvider.IsAvailable)
                return ApiResponseExtensions.ToServerErrorApiResponse<object>("Payment verification is temporarily unavailable. Please try again shortly.");

            var handle = await temporalProvider.Client!.StartOrAttachAsync<IProcessContributionCallbackWorkflow, PaymentCallbackResult>(
                wf => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, null!)),
                $"payment-callback-contribution-{reference}", OperationsTaskQueues.PaymentCallbackProcessing);
            var result = await handle.GetResultAsync();

            // Ownership is checked here, not inside the workflow — the workflow processes
            // a reference on behalf of whichever caller (webhook or this endpoint) reaches
            // it first, with no notion of "who's asking"; only this HTTP layer knows that.
            if (!string.IsNullOrEmpty(memberId))
            {
                var transaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference);
                if (transaction is not null && transaction.MemberId != memberId)
                    return ApiResponseExtensions.ToBadRequestApiResponse<object>("Reference does not belong to the current member");
            }

            return result.IsBadRequest
                ? ApiResponseExtensions.ToBadRequestApiResponse<object>(result.Message)
                : new object().ToOkApiResponse(result.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error verifying Paystack payment reference: {Reference} for member {MemberId}", reference, member?.Id ?? "anonymous");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to verify payment");
        }
    }

    public async Task<IApiResponse<MembershipStatusResponse>> GetMembershipStatusAsync(AuthData member)
    {
        try
        {
            var m = await memberRepo.GetByIdAsync(member.Id);
            if (m is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<MembershipStatusResponse>("Member not found");

            var institutionForPolicy = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            var memberActivePolicy = institutionForPolicy?.MemberActivePolicy;

            var currentYear = DateTime.UtcNow.Year;
            var memberGradYear = m.GraduationYear;

            // All membership campaigns this member could owe, from grad year through current year
            var requiredCampaigns = (await campaignRepo.GetAllAsync(c =>
                    c.IsMembershipCampaign && c.MembershipYear.HasValue
                    && c.MembershipYear.Value >= memberGradYear
                    && c.MembershipYear.Value <= currentYear))
                .ToList();

            if (requiredCampaigns.Count > 0)
            {
                var requiredIds = requiredCampaigns.Select(c => c.Id).ToList();
                var allConfirmed = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.Status == "Successful");
                var paidCampaignIds = allConfirmed
                    .Where(c => requiredIds.Contains(c.CampaignId))
                    .Select(c => c.CampaignId)
                    .Distinct()
                    .ToHashSet();

                // Current year is paid → membership is active, regardless of arrears
                var currentYearCampaigns = requiredCampaigns.Where(c => c.MembershipYear == currentYear).ToList();
                var isCurrentYearPaid = !currentYearCampaigns.Any() || currentYearCampaigns.All(c => paidCampaignIds.Contains(c.Id));

                // Arrears = past-year campaigns that are unpaid
                var unpaidPastCampaigns = requiredCampaigns
                    .Where(c => c.MembershipYear < currentYear && !paidCampaignIds.Contains(c.Id))
                    .ToList();
                var hasArrears = unpaidPastCampaigns.Count > 0;
                var arrearsYears = unpaidPastCampaigns
                    .Where(c => c.MembershipYear.HasValue)
                    .Select(c => c.MembershipYear!.Value)
                    .OrderByDescending(y => y)
                    .ToList();

                var active = MembershipActivityCalculator.ResolveActive(memberActivePolicy, m.Status, isCurrentYearPaid);
                var expiry = isCurrentYearPaid ? new DateTime(currentYear, 12, 31, 23, 59, 59, DateTimeKind.Utc) : (DateTime?)null;
                var yearsPaid = paidCampaignIds.Count;

                var status = new MembershipStatusResponse(active, expiry, yearsPaid, m.LastMembershipPaidAt,
                    isCurrentYearPaid, hasArrears, unpaidPastCampaigns.Count, arrearsYears,
                    memberActivePolicy ?? MembershipActivityCalculator.ApprovedOnlyPolicy);
                return status.ToOkApiResponse();
            }

            // No campaigns configured yet — fall back to member entity fields
            var fallbackStatus = new MembershipStatusResponse(
                MembershipActivityCalculator.ResolveActive(memberActivePolicy, m.Status, m.IsMembershipActive),
                m.MembershipExpiry,
                m.MembershipYearsPaid,
                m.LastMembershipPaidAt,
                memberActivePolicy ?? MembershipActivityCalculator.ApprovedOnlyPolicy);

            return fallbackStatus.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving membership status for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<MembershipStatusResponse>("Failed to retrieve membership status");
        }
    }

    public async Task<IApiResponse<List<CampaignDto>>> GetCurrentYearUnpaidMembershipCampaignsAsync(AuthData member)
    {
        try
        {
            var currentYear = DateTime.UtcNow.Year;
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            var memberGradYear = memberEntity?.GraduationYear ?? 0;

            // Get all membership campaigns from grad year through current year (not future)
            var campaigns = await campaignRepo.GetAllAsync(c =>
                c.IsMembershipCampaign
                && c.MembershipYear.HasValue
                && c.MembershipYear.Value >= memberGradYear
                && c.MembershipYear.Value <= currentYear);

            var confirmed = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.Status == "Successful" && c.Campaign != null);
            var paidIds = new HashSet<string>(confirmed.Select(c => c.CampaignId));

            var unpaid = campaigns
                .Where(c => !paidIds.Contains(c.Id))
                .ToList();

            return unpaid.Select(c => c.ToDto()).ToList().ToOkApiResponse("Unpaid membership campaigns retrieved.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving current-year unpaid membership campaigns for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CampaignDto>>("Failed to retrieve unpaid membership campaigns");
        }
    }

    /// <summary>
    /// Unlike StoreOrder/ServiceRequest, a contribution's PaymentTransaction row isn't
    /// guaranteed to exist yet when the webhook arrives (it's only created once the
    /// Temporal workflow confirms it) — so also check the Redis reference metadata
    /// written at initiation before concluding "not ours".
    /// </summary>
    public async Task<bool> OwnsReferenceAsync(string reference)
    {
        var hasTransaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference, ignoreQueryFilters: true) is not null;
        if (hasTransaction)
            return true;

        return await GetReferenceInfoAsync(reference) is not null;
    }

    public async Task<IApiResponse<ContributionStatusResponse>> GetContributionStatusAsync(string reference, AuthData? member)
    {
        try
        {
            logger.LogInformation("GetContributionStatus for reference {Reference} by member {MemberId}", reference, member?.Id ?? "(guest)");

            // If we already have a payment transaction record, return its status.
            var transaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference);
            if (transaction is not null)
            {
                // A logged-in caller must own the transaction; an anonymous caller (a
                // guest checking their own guest payment) is trusted purely by knowing
                // this reference — same trust model as the [AllowAnonymous] Verify
                // endpoint above, since the reference is an unguessable Paystack id.
                if (member is not null && transaction.MemberId != member.Id)
                    return ApiResponseExtensions.ToBadRequestApiResponse<ContributionStatusResponse>("Reference does not belong to the current member");

                if (transaction.Status == "Pending")
                {
                    return new ContributionStatusResponse
                    {
                        Reference = reference,
                        Status = "Pending",
                        Message = "Payment has been initiated but not yet completed.",
                    }
                    .ToOkApiResponse("Payment pending");
                }

                return new ContributionStatusResponse
                {
                    Reference = reference,
                    Status = transaction.Status,
                    Amount = transaction.Amount,
                    PaymentMethod = transaction.PaymentMethod,
                    Message = transaction.Status == "Successful"
                        ? "Payment confirmed"
                        : transaction.FailureMessage ?? "Payment failed or rejected",
                }
                .ToOkApiResponse("Payment status retrieved");
            }

            // If there is already a recorded contribution (legacy flow), return it immediately.
            // A guest contribution's MemberId is empty, so an anonymous caller (member
            // is null) is only ever matched against those, not another member's rows.
            var contribution = await contributionRepo.GetOneAsync(c => c.TransactionRef == reference && c.MemberId == (member != null ? member.Id : ""));
            if (contribution is not null)
            {
                return new ContributionStatusResponse
                {
                    Reference = reference,
                    Status = contribution.Status,
                    Amount = contribution.Amount,
                    PaymentMethod = contribution.PaymentMethod,
                    Message = contribution.Status == "Successful" ? "Payment confirmed" : "Payment failed or rejected",
                }
                .ToOkApiResponse("Payment status retrieved");
            }

            // Fallback: nothing recorded locally yet — ask the workflow to verify &
            // record (handles cases where a webhook was missed or the cache expired).
            if (temporalProvider.IsAvailable)
            {
                var handle = await temporalProvider.Client!.StartOrAttachAsync<IProcessContributionCallbackWorkflow, PaymentCallbackResult>(
                    wf => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, null!)),
                    $"payment-callback-contribution-{reference}", OperationsTaskQueues.PaymentCallbackProcessing);
                var result = await handle.GetResultAsync();

                transaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference);
                if (transaction is not null)
                {
                    return new ContributionStatusResponse
                    {
                        Reference = reference,
                        Status = transaction.Status,
                        Amount = transaction.Amount,
                        PaymentMethod = transaction.PaymentMethod,
                        Message = transaction.Status == "Successful"
                            ? "Payment confirmed"
                            : transaction.FailureMessage ?? "Payment failed or rejected",
                    }
                    .ToOkApiResponse("Payment status retrieved");
                }

                return new ContributionStatusResponse
                {
                    Reference = reference,
                    Status = "Pending",
                    Message = result.Message,
                }
                .ToOkApiResponse("Payment status retrieved");
            }

            return new ContributionStatusResponse
            {
                Reference = reference,
                Status = "Pending",
                Message = "Payment status could not be verified right now. Please try again shortly.",
            }
            .ToOkApiResponse("Payment status retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving payment status for reference {Reference} and member {MemberId}", reference, member?.Id ?? "(guest)");
            return ApiResponseExtensions.ToServerErrorApiResponse<ContributionStatusResponse>("Failed to retrieve payment status");
        }
    }

    public async Task<IApiResponse<ActivationStatusResponse>> GetActivationStatusAsync(string reference)
    {
        try
        {
            logger.LogInformation("GetActivationStatus (read-only) for reference: {Reference}", reference);

            var transaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference);
            if (transaction is null)
            {
                // Transaction may not have been created yet (webhook not yet received)
                return new ActivationStatusResponse
                {
                    Status = "Pending",
                    Message = "Your payment is still being confirmed. Please wait a moment and try again.",
                }.ToOkApiResponse();
            }

            var email = transaction.Member?.Email;

            // If the member is now Active, also surface their member number
            string? memberNumber = null;
            if (!string.IsNullOrEmpty(transaction.MemberId))
            {
                var memberEntity = await memberRepo.GetByIdAsync(transaction.MemberId);
                if (memberEntity is not null)
                {
                    email ??= memberEntity.Email;
                    if (memberEntity.Status == "Active")
                        memberNumber = memberEntity.MemberNumber;
                }
            }

            return transaction.Status switch
            {
                "Successful" => new ActivationStatusResponse
                {
                    Status = "Successful",
                    Email = email,
                    MemberNumber = memberNumber,
                    Message = "Your membership has been activated successfully.",
                }.ToOkApiResponse(),
                "Failed" => new ActivationStatusResponse
                {
                    Status = "Failed",
                    Email = email,
                    Message = transaction.FailureMessage ?? "Payment could not be completed. Please try again.",
                }.ToOkApiResponse(),
                _ => new ActivationStatusResponse
                {
                    Status = "Pending",
                    Email = email,
                    Message = "Your payment is still being processed. This usually takes a few seconds.",
                }.ToOkApiResponse(),
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving activation status for reference {Reference}", reference);
            return ApiResponseExtensions.ToServerErrorApiResponse<ActivationStatusResponse>("Failed to retrieve activation status");
        }
    }

    public async Task<IApiResponse<ContributionDto>> UploadProofAsync(
        UploadContributionProofRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("UploadProof request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var campaign = await campaignRepo.GetByIdAsync(request.CampaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ContributionDto>("Campaign not found");

            // Members should only pay for membership campaigns with a year >= their graduation year
            if (campaign.IsMembershipCampaign && campaign.MembershipYear.HasValue)
            {
                var memberEntity = await memberRepo.GetByIdAsync(member.Id);
                if (memberEntity is not null && campaign.MembershipYear.Value < memberEntity.GraduationYear)
                    return ApiResponseExtensions.ToBadRequestApiResponse<ContributionDto>("This membership campaign is for a year before your graduation. You are not eligible.");
            }

            var contribution = new Contribution
            {
                CampaignId = request.CampaignId,
                Campaign = new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                MemberId = member.Id,
                Member = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl },
                Amount = 0,
                PaymentMethod = "Manual",
                TransactionRef = request.TransactionRef,
                Notes = request.Notes,
                Status = "Pending",
                CreatedBy = member.Id,
            };
            await contributionRepo.AddAsync(contribution);

            // Notify admins
            var uploadingMember = await memberRepo.GetByIdAsync(member.Id);
            var memberName = uploadingMember is not null ? $"{uploadingMember.FirstName} {uploadingMember.LastName}" : member.Email;
            // HTTP-driven flow (member upload) — currentTenant is already set by
            // TenantResolutionMiddleware, unlike the webhook path above.
            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.PaymentReceivedToAdmins(currentTenant.InstitutionId, memberName, member.Email, 0, campaign.Title, contribution.Id),
                logger);

            logger.LogInformation("Proof uploaded, contribution {ContributionId} created for member {MemberId}", contribution.Id, member.Id);
            return contribution.ToDto().ToCreatedApiResponse("Proof uploaded. Awaiting admin confirmation.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error uploading proof for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ContributionDto>("Failed to upload proof");
        }
    }

    public async Task<IApiResponse<List<RecurringContributionDto>>> GetMyRecurringGivingAsync(string memberId)
    {
        try
        {
            var recurring = await recurringRepo.GetQueryable(r => r.MemberId == memberId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return recurring.Select(r => r.ToDto()).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving recurring gifts for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<RecurringContributionDto>>("Failed to retrieve recurring gifts");
        }
    }

    public async Task<IApiResponse<object>> CancelRecurringGivingAsync(string recurringContributionId, string memberId)
    {
        try
        {
            var recurring = await recurringRepo.GetByIdAsync(recurringContributionId);
            if (recurring is null || recurring.MemberId != memberId)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Recurring gift not found");

            if (recurring.Status is "Cancelled" or "Failed")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("This recurring gift is already stopped");

            recurring.Status = "Cancelled";
            recurring.UpdatedBy = memberId;
            await recurringRepo.UpdateAsync(recurring);

            logger.LogInformation("Member {MemberId} cancelled recurring gift {RecurringId}", memberId, recurring.Id);
            return new object().ToOkApiResponse("Recurring gift cancelled");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error cancelling recurring gift {RecurringId} for member {MemberId}", recurringContributionId, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to cancel recurring gift");
        }
    }
}

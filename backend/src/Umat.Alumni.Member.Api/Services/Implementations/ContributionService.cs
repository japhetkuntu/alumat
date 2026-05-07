using Newtonsoft.Json.Linq;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Options;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.Paystack.Sdk.Models;
using Umat.Alumni.Paystack.Sdk.Services;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Redis.Sdk.Services;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class ContributionService : IContributionService
{
    private readonly IAlumniPgRepository<Contribution> contributionRepo;
    private readonly IAlumniPgRepository<Campaign> campaignRepo;
    private readonly IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member> memberRepo;
    private readonly IAlumniPgRepository<PaymentTransaction> paymentTransactionRepo;
    private readonly IPaystackService paystackService;
    private readonly IRedisService<MemberRedisConfig> redis;
    private readonly ILogger<ContributionService> logger;
    private readonly string _paystackCallbackUrl;

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
                    : new Dictionary<string, Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>();
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
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving contributions for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ContributionDto>>("Failed to retrieve contributions");
        }
    }

    public ContributionService(
        IAlumniPgRepository<Contribution> contributionRepo,
        IAlumniPgRepository<Campaign> campaignRepo,
        IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member> memberRepo,
        IAlumniPgRepository<PaymentTransaction> paymentTransactionRepo,
        IPaystackService paystackService,
        IRedisService<MemberRedisConfig> redis,
        IConfiguration configuration,
        ILogger<ContributionService> logger)
    {
        this.contributionRepo = contributionRepo;
        this.campaignRepo = campaignRepo;
        this.memberRepo = memberRepo;
        this.paymentTransactionRepo = paymentTransactionRepo;
        this.paystackService = paystackService;
        this.redis = redis;
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
            var memberId = member?.Id ?? string.Empty;
            var memberEmail = member?.Email ?? request.Email;

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

            logger.LogInformation("InitiatePaystackPayment request: {Request} by member {MemberId}", request.Serialize(), string.IsNullOrEmpty(memberId) ? "anonymous" : memberId);

            if (string.IsNullOrWhiteSpace(memberEmail))
            {
                // Paystack requires an email, but UI no longer forces it. Use anonymized fallback.
                memberEmail = $"anonymous+{Guid.NewGuid():N}@umat-alumni.org";
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

            var amountInKobo = (long)(request.Amount * 100);

            var response = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Email = memberEmail,
                Amount = amountInKobo,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "memberId", memberId },
                    { "campaignId", request.CampaignId },
                },
            });

            if (!response.Status)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(response.Message);

            var reference = response.Data?.Reference ?? string.Empty;

            // Persist a pending transaction record so we can report back status and store details from the webhook.
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
            };

            await paymentTransactionRepo.AddAsync(transaction);
            await redis.SetAsync($"paystack:ref:{reference}", new PaystackReferenceInfo { MemberId = memberId, CampaignId = request.CampaignId }, TimeSpan.FromHours(24));

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

            var alreadyPaid = await contributionRepo.GetOneAsync(c => c.CampaignId == campaign.Id && c.MemberId == member.Id && c.Status == "Confirmed");
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

            if (!campaign.AllowOnlinePayments)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Online payments are not enabled for membership.");

            var amount = amountPerYear * request.Years;
            var amountInKobo = (long)(amount * 100);
            var response = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Email = member.Email,
                Amount = amountInKobo,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "memberId", member.Id },
                    { "campaignId", campaign.Id },
                    { "membershipYears", request.Years.ToString() }
                },
            });

            if (!response.Status)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(response.Message);

            var reference = response.Data?.Reference ?? string.Empty;

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
            };

            await paymentTransactionRepo.AddAsync(transaction);
            await redis.SetAsync($"paystack:ref:{reference}", new PaystackReferenceInfo { MemberId = member.Id, CampaignId = campaign.Id }, TimeSpan.FromHours(24));

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

    private async Task<IApiResponse<object>> HandlePaystackReferenceAsync(string reference, string? callingMemberId = null, string? rawBody = null)
    {
        // Attempt to load an existing transaction record.
        var transaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference);
        PaystackReferenceInfo? referenceInfo = null;

        if (transaction is null)
        {
            referenceInfo = await GetReferenceInfoAsync(reference);
            if (referenceInfo is null)
            {
                logger.LogWarning("Paystack reference metadata missing for {Reference}. Cannot record contribution.", reference);
                return ApiResponseExtensions.ToOkApiResponse<object>("Payment verified, but metadata missing so contribution was not recorded.");
            }

            if (!string.IsNullOrEmpty(callingMemberId) && callingMemberId != referenceInfo.MemberId)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Reference does not belong to the current member");

            var campaign = await campaignRepo.GetByIdAsync(referenceInfo.CampaignId);
            var member = await memberRepo.GetByIdAsync(referenceInfo.MemberId);

            transaction = new PaymentTransaction
            {
                MemberId = referenceInfo.MemberId,
                Member = member is not null ? new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl } : null,
                CampaignId = referenceInfo.CampaignId,
                Campaign = campaign is not null ? new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title } : null,
                Reference = reference,
                Status = "Pending",
                PaymentMethod = "Paystack",
                CreatedBy = referenceInfo.MemberId,
                CallbackPayload = rawBody,
            };

            await paymentTransactionRepo.AddAsync(transaction);
        }
        else
        {
            if (!string.IsNullOrEmpty(callingMemberId) && callingMemberId != transaction.MemberId)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Reference does not belong to the current member");

            if (!string.IsNullOrEmpty(rawBody))
                transaction.CallbackPayload = rawBody;
        }

        var verifyResponse = await paystackService.VerifyPaymentAsync(reference);
        if (!verifyResponse.Status)
        {
            var friendly = GetFriendlyPaymentFailureMessage(verifyResponse.Message);
            transaction.Status = "Failed";
            transaction.FailureMessage = friendly;
            transaction.ProcessedAt = DateTime.UtcNow;
            await paymentTransactionRepo.UpdateAsync(transaction);
            return ApiResponseExtensions.ToBadRequestApiResponse<object>(friendly);
        }

        var paystackStatus = verifyResponse.Data?.Status?.ToLowerInvariant() ?? "unknown";
        transaction.Amount = (verifyResponse.Data?.Amount ?? 0) / 100m;
        transaction.GatewayResponse = verifyResponse.Data?.GatewayResponse;
        transaction.ProcessedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(rawBody))
        {
            try
            {
                var payload = JObject.Parse(rawBody);
                transaction.Currency ??= payload.SelectToken("data.currency")?.ToString();
                transaction.Channel ??= payload.SelectToken("data.authorization.channel")?.ToString();
            }
            catch
            {
                // best effort; ignore if parsing fails
            }
        }

        if (paystackStatus == "success")
        {
            transaction.Status = "Confirmed";

            var existingContribution = await contributionRepo.GetOneAsync(c => c.TransactionRef == reference && c.MemberId == transaction.MemberId);
            if (existingContribution is null)
            {
                var campaign = await campaignRepo.GetByIdAsync(transaction.CampaignId);

                if (campaign is not null && campaign.IsMembershipCampaign)
                {
                    var priorMembership = await contributionRepo.GetOneAsync(c => c.CampaignId == campaign.Id && c.MemberId == transaction.MemberId && c.Status == "Confirmed");
                    if (priorMembership is not null)
                    {
                        transaction.Status = "Failed";
                        transaction.FailureMessage = "Membership campaign already paid.";
                        transaction.ProcessedAt = DateTime.UtcNow;
                        await paymentTransactionRepo.UpdateAsync(transaction);
                        await redis.RemoveAsync($"paystack:ref:{reference}");
                        return ApiResponseExtensions.ToBadRequestApiResponse<object>("Membership campaign has already been paid.");
                    }
                }

                var memberSnapshot = transaction.Member;
                if (memberSnapshot is null && !string.IsNullOrEmpty(transaction.MemberId))
                {
                    var member = await memberRepo.GetByIdAsync(transaction.MemberId);
                    if (member is not null)
                    {
                        memberSnapshot = new MemberSnapshot
                        {
                            Id = member.Id,
                            FirstName = member.FirstName,
                            LastName = member.LastName,
                            Email = member.Email,
                            ProfilePictureUrl = member.ProfilePictureUrl,
                        };
                    }
                }

                var contribution = new Contribution
                {
                    MemberId = transaction.MemberId,
                    CampaignId = transaction.CampaignId,
                    Member = memberSnapshot,
                    Campaign = campaign is not null ? new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title } : null,
                    Amount = transaction.Amount,
                    PaymentMethod = "Paystack",
                    TransactionRef = reference,
                    Status = "Confirmed",
                    ConfirmedAt = DateTime.UtcNow,
                    ConfirmedBy = "Paystack",
                    CreatedBy = transaction.MemberId,
                };

                await contributionRepo.AddAsync(contribution);

                if (campaign is not null)
                {
                    campaign.CollectedAmount += contribution.Amount;
                    campaign.PaidCount += 1;
                    await campaignRepo.UpdateAsync(campaign);

                    if (campaign.IsMembershipCampaign && !string.IsNullOrEmpty(transaction.MemberId))
                    {
                        var memberToUpdate = await memberRepo.GetByIdAsync(transaction.MemberId);
                        if (memberToUpdate is not null)
                        {
                            var now = DateTime.UtcNow;
                            var currentYear = now.Year;
                            var gradYear = memberToUpdate.GraduationYear;

                            // Re-evaluate full membership: active = paid ALL campaigns from grad year through current year
                            var requiredCampaigns = await campaignRepo.GetAllAsync(c =>
                                c.IsMembershipCampaign && c.MembershipYear.HasValue
                                && c.MembershipYear.Value >= gradYear
                                && c.MembershipYear.Value <= currentYear);

                            var confirmedContributions = await contributionRepo.GetAllAsync(c =>
                                c.MemberId == transaction.MemberId && c.Status == "Confirmed");
                            var paidCampaignIds = new HashSet<string>(confirmedContributions.Select(c => c.CampaignId));
                            var allPaid = requiredCampaigns.All(c => paidCampaignIds.Contains(c.Id));

                            memberToUpdate.IsMembershipActive = allPaid;
                            memberToUpdate.MembershipExpiry = allPaid
                                ? new DateTime(currentYear, 12, 31, 23, 59, 59, DateTimeKind.Utc)
                                : null;
                            memberToUpdate.MembershipYearsPaid = requiredCampaigns.Count(c => paidCampaignIds.Contains(c.Id));
                            memberToUpdate.LastMembershipPaidAt = now;

                            // Auto-approve pending members who confirmed their membership payment
                            if (memberToUpdate.Status == "Pending")
                            {
                                if (string.IsNullOrEmpty(memberToUpdate.MemberNumber))
                                {
                                    var prefix = $"UMaT-{memberToUpdate.GraduationYear}-";
                                    var existingWithNumber = await memberRepo.GetAllAsync(m => m.MemberNumber != null && m.MemberNumber.StartsWith(prefix));
                                    var maxSeq = existingWithNumber
                                        .Select(m => int.TryParse(m.MemberNumber![(prefix.Length)..], out var n) ? n : 0)
                                        .DefaultIfEmpty(0)
                                        .Max();
                                    memberToUpdate.MemberNumber = $"{prefix}{(maxSeq + 1):D4}";
                                }
                                memberToUpdate.Status = "Active";
                                memberToUpdate.UpdatedAt = DateTime.UtcNow;
                                memberToUpdate.UpdatedBy = "system";
                                logger.LogInformation("Auto-approved pending member {MemberId} with number {MemberNumber} after membership payment", memberToUpdate.Id, memberToUpdate.MemberNumber);
                            }

                            await memberRepo.UpdateAsync(memberToUpdate);
                        }
                    }
                }
            }

            await paymentTransactionRepo.UpdateAsync(transaction);
            await redis.RemoveAsync($"paystack:ref:{reference}");

            logger.LogInformation("Paystack payment verified and contribution recorded for member {MemberId}", transaction.MemberId);
            return new object().ToOkApiResponse("Payment verified and contribution recorded");
        }

        transaction.Status = paystackStatus == "pending" ? "Pending" : "Failed";
        await paymentTransactionRepo.UpdateAsync(transaction);

        if (transaction.Status != "Pending")
            await redis.RemoveAsync($"paystack:ref:{reference}");

        return ApiResponseExtensions.ToOkApiResponse<object>("Payment status updated");
    }

    private static string GetFriendlyPaymentFailureMessage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "We couldn't confirm your payment. Please try again or contact support.";

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Contains("invalid reference") || normalized.Contains("could not find"))
            return "We couldn't find that payment reference. Please try again.";

        if (normalized.Contains("already verified") || normalized.Contains("already been verified"))
            return "This payment has already been processed.";

        if (normalized.Contains("insufficient funds") || normalized.Contains("card declined"))
            return "Your card was declined. Please check with your bank or try another payment method.";

        if (normalized.Contains("expired") || normalized.Contains("expired card"))
            return "Your payment method has expired. Please use a different card.";

        if (normalized.Contains("not authorised") || normalized.Contains("authorization"))
            return "The payment was not authorized. Please try again or use another payment method.";

        return "We couldn't confirm your payment. Please try again or contact support.";
    }

    public async Task<IApiResponse<object>> VerifyPaystackPaymentAsync(string reference, AuthData? member)
    {
        try
        {
            var memberId = member?.Id;
            logger.LogInformation("VerifyPaystackPayment for reference: {Reference}, member {MemberId}", reference, memberId ?? "anonymous");
            return await HandlePaystackReferenceAsync(reference, memberId);
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
                var allConfirmed = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.Status == "Confirmed");
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
                    .OrderBy(y => y)
                    .ToList();

                var active = isCurrentYearPaid;
                var expiry = active ? new DateTime(currentYear, 12, 31, 23, 59, 59, DateTimeKind.Utc) : (DateTime?)null;
                var yearsPaid = paidCampaignIds.Count;

                var status = new MembershipStatusResponse(active, expiry, yearsPaid, m.LastMembershipPaidAt,
                    isCurrentYearPaid, hasArrears, unpaidPastCampaigns.Count, arrearsYears);
                return status.ToOkApiResponse();
            }

            // No campaigns configured yet — fall back to member entity fields
            var fallbackStatus = new MembershipStatusResponse(
                m.IsMembershipActive,
                m.MembershipExpiry,
                m.MembershipYearsPaid,
                m.LastMembershipPaidAt);

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

            var confirmed = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.Status == "Confirmed" && c.Campaign != null);
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

    public async Task<IApiResponse<object>> ProcessPaystackCallbackAsync(string reference, string rawBody)
    {
        try
        {
            logger.LogInformation("ProcessPaystackCallback for reference: {Reference}", reference);
            return await HandlePaystackReferenceAsync(reference, rawBody: rawBody);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing Paystack callback reference: {Reference}", reference);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to process callback");
        }
    }

    public async Task<IApiResponse<ContributionStatusResponse>> GetContributionStatusAsync(string reference, AuthData member)
    {
        try
        {
            logger.LogInformation("GetContributionStatus for reference {Reference} by member {MemberId}", reference, member.Id);

            // If we already have a payment transaction record, return its status.
            var transaction = await paymentTransactionRepo.GetOneAsync(t => t.Reference == reference);
            if (transaction is not null)
            {
                if (transaction.MemberId != member.Id)
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
                    Message = transaction.Status == "Confirmed"
                        ? "Payment confirmed"
                        : transaction.FailureMessage ?? "Payment failed or rejected",
                }
                .ToOkApiResponse("Payment status retrieved");
            }

            // If there is already a recorded contribution (legacy flow), return it immediately.
            var contribution = await contributionRepo.GetOneAsync(c => c.TransactionRef == reference && c.MemberId == member.Id);
            if (contribution is not null)
            {
                return new ContributionStatusResponse
                {
                    Reference = reference,
                    Status = contribution.Status,
                    Amount = contribution.Amount,
                    PaymentMethod = contribution.PaymentMethod,
                    Message = contribution.Status == "Confirmed" ? "Payment confirmed" : "Payment failed or rejected",
                }
                .ToOkApiResponse("Payment status retrieved");
            }

            // Fallback: query Paystack directly for status (handles cases where a webhook was missed or the cache expired)
            var verifyResponse = await paystackService.VerifyPaymentAsync(reference);
            if (!verifyResponse.Status)
            {
                return new ContributionStatusResponse
                {
                    Reference = reference,
                    Status = "Failed",
                    Message = verifyResponse.Message,
                }
                .ToOkApiResponse("Payment status retrieved");
            }

            var paystackStatus = verifyResponse.Data?.Status?.ToLowerInvariant() ?? "unknown";

            if (paystackStatus == "success")
            {
                // Ensure we record the contribution if it hasn't been created yet.
                await HandlePaystackReferenceAsync(reference);
                return new ContributionStatusResponse
                {
                    Reference = reference,
                    Status = "Confirmed",
                    Amount = (verifyResponse.Data?.Amount ?? 0) / 100m,
                    PaymentMethod = "Paystack",
                    Message = "Payment confirmed",
                }
                .ToOkApiResponse("Payment confirmed");
            }

            return new ContributionStatusResponse
            {
                Reference = reference,
                Status = paystackStatus,
                Amount = verifyResponse.Data?.Amount / 100m,
                PaymentMethod = "Paystack",
                Message = "Payment not completed yet.",
            }
            .ToOkApiResponse("Payment status retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving payment status for reference {Reference} and member {MemberId}", reference, member.Id);
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
                "Confirmed" => new ActivationStatusResponse
                {
                    Status = "Confirmed",
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

            logger.LogInformation("Proof uploaded, contribution {ContributionId} created for member {MemberId}", contribution.Id, member.Id);
            return contribution.ToDto().ToCreatedApiResponse("Proof uploaded. Awaiting admin confirmation.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error uploading proof for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ContributionDto>("Failed to upload proof");
        }
    }
}

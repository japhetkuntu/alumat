using Microsoft.Extensions.Options;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class ReferralService(
    IAlumniPgRepository<Referral> referralRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<MemberBadge> badgeRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ICurrentTenantService currentTenant,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    ITemporalClientProvider temporalProvider,
    ILogger<ReferralService> logger) : IReferralService
{
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;

    /// <summary>The current tenant's own name/color/logo for tenant-branded email — falls back to the platform default when unset.</summary>
    private async Task<(string? Name, string? Color, string? SecondaryColor, string? Logo)> GetBrandVarsAsync()
    {
        if (string.IsNullOrEmpty(currentTenant.InstitutionId)) return (null, null, null, null);
        var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        var name = string.IsNullOrWhiteSpace(institution?.PortalName) ? institution?.Name : institution.PortalName;
        return (name, institution?.PrimaryColorHex, institution?.SecondaryColorHex, institution?.LogoUrl);
    }

    /// <summary>
    /// The current request's own scheme+host — see the identical helper in
    /// MemberAuthService for why this can't be a fixed domain.
    /// </summary>
    private string GetRequestBaseUrl()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? "https://example.com" : $"{request.Scheme}://{request.Host}";
    }

    /// <summary>Points earned from one referral row, by its current status — see ReferralPoints.
    /// A "MembershipPaid" referral is worth the registration points plus the bonus, not the
    /// bonus alone, since it passed through "Registered" on the way there.</summary>
    private static int PointsFor(string status) => status switch
    {
        "Registered" => ReferralPoints.Registered,
        "MembershipPaid" => ReferralPoints.Registered + ReferralPoints.MembershipBonus,
        _ => 0,
    };

    public async Task<IApiResponse<ReferralInfoDto>> GetMyReferralInfoAsync(AuthData member)
    {
        try
        {
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ReferralInfoDto>("Member not found");

            // Generate referral code if not set
            if (string.IsNullOrEmpty(memberEntity.ReferralCode))
            {
                memberEntity.ReferralCode = GenerateReferralCode(member.FirstName, member.LastName);
                await memberRepo.UpdateAsync(memberEntity);
            }

            var referrals = await referralRepo.GetAllAsync(r => r.ReferrerId == member.Id);
            var referralList = referrals.ToList();
            var points = referralList.Sum(r => PointsFor(r.Status));

            var hasBadge = await badgeRepo.GetOneAsync(b => b.MemberId == member.Id && b.BadgeType == "Referrer");

            // Rank among every referrer institution-wide with at least one point — a plain
            // count query per referrer would be one query per member; instead pull just the
            // (ReferrerId, Status) pairs once and aggregate in memory, same pattern GetLeaderboardAsync uses.
            var allReferrals = await referralRepo.GetAllAsync();
            var pointsByReferrer = allReferrals.GroupBy(r => r.ReferrerId).ToDictionary(g => g.Key, g => g.Sum(r => PointsFor(r.Status)));
            int? rank = null;
            if (points > 0)
            {
                rank = pointsByReferrer.Values.Count(p => p > points) + 1;
            }

            var info = new ReferralInfoDto
            {
                ReferralCode = memberEntity.ReferralCode,
                TotalReferrals = referralList.Count,
                RegisteredReferrals = referralList.Count(r => r.Status is "Registered" or "MembershipPaid"),
                PendingReferrals = referralList.Count(r => r.Status == "Pending"),
                MembershipPaidReferrals = referralList.Count(r => r.Status == "MembershipPaid"),
                Points = points,
                Rank = rank,
                HasReferrerBadge = hasBadge is not null,
            };

            return info.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving referral info for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ReferralInfoDto>("Failed to retrieve referral info");
        }
    }

    public async Task<IApiResponse<List<ReferralLeaderboardEntryDto>>> GetLeaderboardAsync()
    {
        try
        {
            const int top = 20;
            var allReferrals = (await referralRepo.GetAllAsync()).ToList();
            var byReferrer = allReferrals
                .GroupBy(r => r.ReferrerId)
                .Select(g => new
                {
                    ReferrerId = g.Key,
                    Points = g.Sum(r => PointsFor(r.Status)),
                    TotalReferrals = g.Count(r => r.Status is "Registered" or "MembershipPaid"),
                    MembershipPaidReferrals = g.Count(r => r.Status == "MembershipPaid"),
                })
                .Where(x => x.Points > 0)
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.MembershipPaidReferrals)
                .Take(top)
                .ToList();

            var referrerIds = byReferrer.Select(x => x.ReferrerId).ToList();
            var referrers = referrerIds.Count > 0
                ? (await memberRepo.GetAllAsync(m => referrerIds.Contains(m.Id))).ToDictionary(m => m.Id)
                : new Dictionary<string, MemberEntity>();

            var leaderboard = byReferrer
                .Select((x, i) =>
                {
                    var m = referrers.GetValueOrDefault(x.ReferrerId);
                    return new ReferralLeaderboardEntryDto
                    {
                        Rank = i + 1,
                        MemberId = x.ReferrerId,
                        Name = m is null ? "Former member" : $"{m.FirstName} {m.LastName}",
                        ProfilePictureUrl = m?.ProfilePictureUrl,
                        Points = x.Points,
                        TotalReferrals = x.TotalReferrals,
                        MembershipPaidReferrals = x.MembershipPaidReferrals,
                    };
                })
                .ToList();

            return leaderboard.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error generating referral leaderboard");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<ReferralLeaderboardEntryDto>>("Failed to generate leaderboard");
        }
    }

    public async Task<IApiResponse<object>> InviteAsync(string email, AuthData member)
    {
        try
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            // Check if already a member
            var existingMember = await memberRepo.GetOneAsync(m => m.Email == normalizedEmail);
            if (existingMember is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("This person is already a registered member.");

            // Check if already referred by this member
            var existingReferral = await referralRepo.GetOneAsync(r => r.ReferrerId == member.Id && r.ReferredEmail == normalizedEmail);
            if (existingReferral is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You've already sent an invitation to this email.");

            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            // Generate referral code if needed
            if (string.IsNullOrEmpty(memberEntity.ReferralCode))
            {
                memberEntity.ReferralCode = GenerateReferralCode(member.FirstName, member.LastName);
                await memberRepo.UpdateAsync(memberEntity);
            }

            var referral = new Referral
            {
                ReferrerId = member.Id,
                Referrer = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                },
                ReferredEmail = normalizedEmail,
                Status = "Pending",
                CreatedBy = member.Id,
            };

            await referralRepo.AddAsync(referral);

            // The actual send happens off-request in the notification actor —
            // skipped entirely when the institution has email notifications
            // turned off (cost control); the referral itself is still tracked
            // above regardless, the referrer can still share their code manually.
            var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            if (institution?.EmailNotificationsEnabled == false)
            {
                logger.LogInformation("Referral invitation email to {Email} skipped — email notifications are off for this institution", normalizedEmail);
                return ((object)new { Message = "Invitation recorded." }).ToCreatedApiResponse("Invitation recorded.");
            }

            var brand = await GetBrandVarsAsync();
            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.Email(
                    new SendEmailRequest
                    {
                        To = [new EmailContact { Email = normalizedEmail }],
                        TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.ReferralInvitation)
                            ? "referral-invitation"
                            : mailtrapConfig.Templates.ReferralInvitation,
                        TemplateVariables = new
                        {
                            referrer_name = member.Name,
                            referral_code = memberEntity.ReferralCode,
                            register_url = $"{GetRequestBaseUrl()}/register?ref={memberEntity.ReferralCode}",
                            brand_name = brand.Name,
                            brand_color = brand.Color,
                            brand_secondary_color = brand.SecondaryColor,
                            brand_logo = brand.Logo,
                        },
                    },
                    $"referral invitation email to {normalizedEmail}"),
                logger);

            return ((object)new { Message = "Invitation sent successfully." }).ToCreatedApiResponse("Invitation sent.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending referral invite for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to send invitation");
        }
    }

    public async Task<IApiResponse<List<ReferralDto>>> GetMyReferralsAsync(string memberId)
    {
        try
        {
            var referrals = await referralRepo.GetAllAsync(r => r.ReferrerId == memberId);
            var dtos = referrals.Select(r => r.ToDto()).OrderByDescending(r => r.CreatedAt).ToList();

            // ToDto() reads names from the MemberSnapshots frozen at referral time —
            // ReferrerName is always the same one member (the caller, a single
            // lookup), but ReferredMemberName is a different person per row, so
            // that's a batched lookup, not one query per row.
            var caller = await memberRepo.GetByIdAsync(memberId);
            var referredIds = dtos.Where(d => d.ReferredMemberId != null).Select(d => d.ReferredMemberId!).Distinct().ToList();
            var referredMembers = referredIds.Count > 0
                ? (await memberRepo.GetAllAsync(m => referredIds.Contains(m.Id))).ToDictionary(m => m.Id)
                : new Dictionary<string, MemberEntity>();
            foreach (var dto in dtos)
            {
                if (caller is not null)
                    dto.ReferrerName = $"{caller.FirstName} {caller.LastName}";
                if (dto.ReferredMemberId != null && referredMembers.TryGetValue(dto.ReferredMemberId, out var rm))
                    dto.ReferredMemberName = $"{rm.FirstName} {rm.LastName}";
            }

            return dtos.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving referrals for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<ReferralDto>>("Failed to retrieve referrals");
        }
    }

    private static string GenerateReferralCode(string firstName, string lastName)
    {
        var prefix = $"{firstName[..Math.Min(3, firstName.Length)]}{lastName[..Math.Min(3, lastName.Length)]}".ToUpperInvariant();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"{prefix}-{suffix}";
    }
}

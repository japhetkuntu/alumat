using Microsoft.Extensions.Options;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Member.Api.Actors;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
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
    INotificationActor notificationActor,
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

    public async Task<IApiResponse<object>> GetMyReferralInfoAsync(AuthData member)
    {
        try
        {
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            // Generate referral code if not set
            if (string.IsNullOrEmpty(memberEntity.ReferralCode))
            {
                memberEntity.ReferralCode = GenerateReferralCode(member.FirstName, member.LastName);
                await memberRepo.UpdateAsync(memberEntity);
            }

            var referrals = await referralRepo.GetAllAsync(r => r.ReferrerId == member.Id);
            var referralList = referrals.ToList();

            var hasBadge = await badgeRepo.GetOneAsync(b => b.MemberId == member.Id && b.BadgeType == "Referrer");

            var info = (object)new
            {
                ReferralCode = memberEntity.ReferralCode,
                TotalReferrals = referralList.Count,
                RegisteredReferrals = referralList.Count(r => r.Status is "Registered" or "MembershipPaid"),
                PendingReferrals = referralList.Count(r => r.Status == "Pending"),
                HasReferrerBadge = hasBadge is not null,
            };

            return info.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving referral info for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to retrieve referral info");
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

            // The actual send happens off-request in the notification actor.
            var brand = await GetBrandVarsAsync();
            notificationActor.Tell(new SendEmailCommand(
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
                $"referral invitation email to {normalizedEmail}"));

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
            return referrals.Select(r => r.ToDto()).OrderByDescending(r => r.CreatedAt).ToList().ToOkApiResponse();
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

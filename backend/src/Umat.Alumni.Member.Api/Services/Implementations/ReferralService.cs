using Microsoft.Extensions.Options;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Mailtrap.Sdk.Models;
using Umat.Alumni.Mailtrap.Sdk.Options;
using Umat.Alumni.Mailtrap.Sdk.Services;
using Umat.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class ReferralService(
    IAlumniPgRepository<Referral> referralRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<MemberBadge> badgeRepo,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    IEmailService emailService,
    ILogger<ReferralService> logger) : IReferralService
{
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;

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

            // Send invitation email
            try
            {
                await emailService.SendEmailAsync(new SendEmailRequest
                {
                    To = [new EmailContact { Email = normalizedEmail }],
                    TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.ReferralInvitation)
                        ? "referral-invitation"
                        : mailtrapConfig.Templates.ReferralInvitation,
                    TemplateVariables = new
                    {
                        referrer_name = member.Name,
                        referral_code = memberEntity.ReferralCode,
                        register_url = $"https://alumat.umat.edu.gh/register?ref={memberEntity.ReferralCode}",
                    },
                });
            }
            catch (Exception emailEx)
            {
                logger.LogWarning(emailEx, "Failed to send referral email to {Email}", normalizedEmail);
            }

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

using Microsoft.EntityFrameworkCore;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class BadgeService(
    IAlumniPgRepository<MemberBadge> badgeRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Referral> referralRepo,
    ILogger<BadgeService> logger) : IBadgeService
{
    public async Task<IApiResponse<List<MemberBadgeDto>>> GetMyBadgesAsync(string memberId)
    {
        try
        {
            var badges = await badgeRepo.GetAllAsync(b => b.MemberId == memberId);
            return badges.Select(b => b.ToDto()).OrderByDescending(b => b.EarnedAt).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving badges for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<MemberBadgeDto>>("Failed to retrieve badges");
        }
    }

    public async Task EvaluateAndAwardBadgesAsync(string memberId)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null) return;

            var existingBadges = (await badgeRepo.GetAllAsync(b => b.MemberId == memberId))
                .Select(b => b.BadgeType).ToHashSet();

            var snapshot = new MemberSnapshot
            {
                Id = member.Id,
                FirstName = member.FirstName,
                LastName = member.LastName,
                Email = member.Email,
                ProfilePictureUrl = member.ProfilePictureUrl,
            };

            var newBadges = new List<MemberBadge>();

            // First Contribution
            if (!existingBadges.Contains("FirstContribution"))
            {
                var hasContribution = await contributionRepo.GetOneAsync(c => c.MemberId == memberId && c.Status == "Confirmed");
                if (hasContribution is not null)
                    newBadges.Add(CreateBadge(memberId, snapshot, "FirstContribution", "Made your first contribution"));
            }

            // Event Attendee (3+ events)
            if (!existingBadges.Contains("EventAttendee3"))
            {
                var rsvpCount = await rsvpRepo.CountAsync(r => r.MemberId == memberId && r.Status == "Confirmed");
                if (rsvpCount >= 3)
                    newBadges.Add(CreateBadge(memberId, snapshot, "EventAttendee3", "Attended 3 or more events"));
            }

            // Referrer (1+ successful referral)
            if (!existingBadges.Contains("Referrer"))
            {
                var referralCount = await referralRepo.CountAsync(r => r.ReferrerId == memberId && r.Status == "Registered");
                if (referralCount >= 1)
                    newBadges.Add(CreateBadge(memberId, snapshot, "Referrer", "Referred a fellow alumnus"));
            }

            // Super Referrer (5+ successful referrals)
            if (!existingBadges.Contains("SuperReferrer"))
            {
                var referralCount = await referralRepo.CountAsync(r => r.ReferrerId == memberId && r.Status == "Registered");
                if (referralCount >= 5)
                    newBadges.Add(CreateBadge(memberId, snapshot, "SuperReferrer", "Referred 5 or more alumni"));
            }

            // Membership streak badges
            var membershipCampaignIds = await campaignRepo
                .GetQueryable(c => c.IsMembershipCampaign && c.MembershipYear.HasValue)
                .Select(c => new { c.Id, c.MembershipYear })
                .ToListAsync();

            var paidMembershipYears = await contributionRepo
                .GetQueryable(c => c.MemberId == memberId && c.Status == "Confirmed" && membershipCampaignIds.Select(mc => mc.Id).Contains(c.CampaignId))
                .Select(c => c.CampaignId)
                .ToListAsync();

            var yearsPaid = membershipCampaignIds.Where(mc => paidMembershipYears.Contains(mc.Id)).Select(mc => mc.MembershipYear!.Value).Distinct().OrderBy(y => y).ToList();
            var streak = CalculateStreak(yearsPaid);

            if (streak >= 2 && !existingBadges.Contains("MembershipStreak2"))
                newBadges.Add(CreateBadge(memberId, snapshot, "MembershipStreak2", "2 consecutive years of membership"));
            if (streak >= 3 && !existingBadges.Contains("MembershipStreak3"))
                newBadges.Add(CreateBadge(memberId, snapshot, "MembershipStreak3", "3 consecutive years of membership"));
            if (streak >= 5 && !existingBadges.Contains("MembershipStreak5"))
                newBadges.Add(CreateBadge(memberId, snapshot, "MembershipStreak5", "5 consecutive years of membership"));

            if (newBadges.Count > 0)
                await badgeRepo.AddRangeAsync(newBadges);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error evaluating badges for member {MemberId}", memberId);
        }
    }

    private static int CalculateStreak(List<int> sortedYears)
    {
        if (sortedYears.Count == 0) return 0;

        var currentYear = DateTime.UtcNow.Year;
        var maxStreak = 0;
        var streak = 1;

        for (var i = sortedYears.Count - 1; i > 0; i--)
        {
            if (sortedYears[i] - sortedYears[i - 1] == 1)
                streak++;
            else
                break;
        }

        // Only count if streak reaches current year or last year
        if (sortedYears.Count > 0 && sortedYears[^1] >= currentYear - 1)
            maxStreak = streak;

        return maxStreak;
    }

    private static MemberBadge CreateBadge(string memberId, MemberSnapshot snapshot, string type, string description) => new()
    {
        MemberId = memberId,
        Member = snapshot,
        BadgeType = type,
        Description = description,
        EarnedAt = DateTime.UtcNow,
        CreatedBy = memberId,
    };
}

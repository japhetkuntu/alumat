using Microsoft.EntityFrameworkCore;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Member.Api.Services.Implementations;

public class LeaderboardService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    ILogger<LeaderboardService> logger) : ILeaderboardService
{
    public async Task<IApiResponse<List<YearGroupLeaderboardEntryDto>>> GetLeaderboardAsync()
    {
        try
        {
            var currentYear = DateTime.UtcNow.Year;

            // Get all active members grouped by graduation year
            var members = await memberRepo.GetQueryable(m => m.Status == "Active")
                .GroupBy(m => m.GraduationYear)
                .Select(g => new { YearGroup = g.Key, Count = g.Count() })
                .ToListAsync();

            // Get current-year membership campaign IDs
            var membershipCampaignIds = await campaignRepo
                .GetQueryable(c => c.IsMembershipCampaign && c.MembershipYear == currentYear)
                .Select(c => c.Id)
                .ToListAsync();

            // Get confirmed membership contributions grouped by member's year group
            var membershipPaid = membershipCampaignIds.Count > 0
                ? await contributionRepo.GetQueryable(c => c.Status == "Confirmed" && membershipCampaignIds.Contains(c.CampaignId))
                    .Join(memberRepo.GetQueryable(), c => c.MemberId, m => m.Id, (c, m) => m.GraduationYear)
                    .GroupBy(y => y)
                    .Select(g => new { YearGroup = g.Key, Count = g.Count() })
                    .ToListAsync()
                : [];

            // Get total confirmed contributions grouped by member year group
            var totalContributed = await contributionRepo.GetQueryable(c => c.Status == "Confirmed")
                .Join(memberRepo.GetQueryable(), c => c.MemberId, m => m.Id, (c, m) => new { m.GraduationYear, c.Amount })
                .GroupBy(x => x.GraduationYear)
                .Select(g => new { YearGroup = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            // Get event attendance (confirmed RSVPs) grouped by member year group
            var eventAttendance = await rsvpRepo.GetQueryable(r => r.Status == "Confirmed")
                .Join(memberRepo.GetQueryable(), r => r.MemberId, m => m.Id, (r, m) => m.GraduationYear)
                .GroupBy(y => y)
                .Select(g => new { YearGroup = g.Key, Count = g.Count() })
                .ToListAsync();

            var paidLookup = membershipPaid.ToDictionary(x => x.YearGroup, x => x.Count);
            var contributedLookup = totalContributed.ToDictionary(x => x.YearGroup, x => x.Total);
            var attendanceLookup = eventAttendance.ToDictionary(x => x.YearGroup, x => x.Count);

            var leaderboard = members.Select(m =>
            {
                var paid = paidLookup.GetValueOrDefault(m.YearGroup, 0);
                var rate = m.Count > 0 ? Math.Round((decimal)paid / m.Count * 100, 1) : 0;
                return new YearGroupLeaderboardEntryDto
                {
                    YearGroup = m.YearGroup,
                    TotalMembers = m.Count,
                    MembershipPaidCount = paid,
                    MembershipRate = rate,
                    TotalContributed = contributedLookup.GetValueOrDefault(m.YearGroup, 0),
                    EventAttendanceCount = attendanceLookup.GetValueOrDefault(m.YearGroup, 0),
                };
            })
            .OrderByDescending(e => e.MembershipRate)
            .ThenByDescending(e => e.TotalContributed)
            .ToList();

            return leaderboard.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error generating leaderboard");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<YearGroupLeaderboardEntryDto>>("Failed to generate leaderboard");
        }
    }
}

using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Polls;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Voting from the member side. A member sees only polls their audience includes, votes once with a whole ballot, and sees figures only
/// when the poll's results mode says they may. What they chose is never stored against them: see <see cref="PollEngine"/>.
/// </summary>
public class MemberPollService(
    IAlumniPgRepository<Poll> pollRepo,
    IAlumniPgRepository<PollPosition> positionRepo,
    IAlumniPgRepository<PollAspirant> aspirantRepo,
    IAlumniPgRepository<PollParticipation> participationRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    PollEngine engine,
    ILogger<MemberPollService> logger) : IMemberPollService
{
    private async Task<(MemberEntity? Member, HashSet<string> Groups)> WhoAsync(string memberId)
    {
        var member = await memberRepo.GetByIdAsync(memberId);
        var groups = (await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved")).Select(m => m.CommunityId).ToHashSet();
        return (member, groups);
    }

    private static bool CanSee(Poll p, MemberEntity member, HashSet<string> groups) =>
        p.Status == PollStatuses.Published && member.Status == "Active"
        && PollRules.InAudience(p.YearGroups, p.CommunityId, member.GraduationYear, p.CommunityId is not null && groups.Contains(p.CommunityId));

    private async Task<string> AudienceLabelAsync(Poll p)
    {
        if (!string.IsNullOrEmpty(p.CommunityId)) return (await communityRepo.GetByIdAsync(p.CommunityId))?.Name ?? "A group";
        if (p.YearGroups is { Count: > 0 }) return p.YearGroups.Count == 1 ? $"Class of {p.YearGroups[0]}" : string.Join(", ", p.YearGroups.OrderBy(y => y));
        return "Everyone";
    }

    public async Task<IApiResponse<List<MemberPollListItem>>> GetPollsAsync(AuthData auth)
    {
        try
        {
            var (member, groups) = await WhoAsync(auth.Id);
            if (member is null) return new List<MemberPollListItem>().ToOkApiResponse();
            var now = DateTime.UtcNow;
            // Polls closed long ago stay reachable by link but do not crowd the list.
            var recent = now.AddDays(-90);
            var polls = (await pollRepo.GetAllAsync(p => p.Status == PollStatuses.Published && p.ArchivedAt == null && p.ClosesAt >= recent))
                .Where(p => CanSee(p, member, groups)).ToList();
            if (polls.Count == 0) return new List<MemberPollListItem>().ToOkApiResponse();

            var ids = polls.Select(p => p.Id).ToList();
            var votedIds = (await participationRepo.GetAllAsync(p => p.MemberId == auth.Id && ids.Contains(p.PollId))).Select(p => p.PollId).ToHashSet();
            var counts = (await positionRepo.GetAllAsync(p => ids.Contains(p.PollId))).GroupBy(p => p.PollId).ToDictionary(g => g.Key, g => g.Count());

            var items = new List<MemberPollListItem>();
            foreach (var p in polls)
                items.Add(new MemberPollListItem(
                    p.Id, p.Title, p.Description, PollRules.StateOf(p.Status, p.OpensAt, p.ClosesAt, now), p.OpensAt, p.ClosesAt,
                    await AudienceLabelAsync(p), counts.GetValueOrDefault(p.Id), votedIds.Contains(p.Id),
                    PollRules.ResultsVisible(p.Status, p.ResultsMode, p.OpensAt, p.ClosesAt, p.ResultsPublishedAt, now)));

            // Open first (soonest to close), then what is coming, then what has finished (newest first).
            int Rank(string s) => s switch { PollStates.Open => 0, PollStates.Scheduled => 1, _ => 2 };
            return items.OrderBy(i => Rank(i.State)).ThenBy(i => i.State == PollStates.Closed ? 0 : 1).ThenBy(i => i.State == PollStates.Closed ? -i.ClosesAt.Ticks : i.ClosesAt.Ticks).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing polls for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<MemberPollListItem>>("Failed to load polls");
        }
    }

    public async Task<IApiResponse<MemberPollDetail>> GetPollAsync(string id, AuthData auth)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            var (member, groups) = await WhoAsync(auth.Id);
            if (poll is null || member is null || !CanSee(poll, member, groups))
                return ApiResponseExtensions.ToNotFoundApiResponse<MemberPollDetail>("Poll not found");

            var now = DateTime.UtcNow;
            var state = PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, now);
            var mark = await participationRepo.GetOneAsync(p => p.PollId == id && p.MemberId == auth.Id);
            var positions = (await positionRepo.GetAllAsync(p => p.PollId == id)).OrderBy(p => p.SortOrder).ToList();
            var aspirants = (await aspirantRepo.GetAllAsync(a => a.PollId == id)).OrderBy(a => a.SortOrder).ToList();
            var dto = positions.Select(p =>
            {
                var mine = aspirants.Where(a => a.PositionId == p.Id).Select(a => new MemberAspirantDto(a.Id, a.Name, a.Details, a.PhotoUrl, a.MemberId)).ToList();
                return new MemberPositionDto(p.Id, p.Title, p.Description, PollRules.IsYesNo(mine.Count), mine);
            }).ToList();

            return new MemberPollDetail(
                poll.Id, poll.Title, poll.Description, state, poll.OpensAt, poll.ClosesAt, await AudienceLabelAsync(poll),
                mark is not null, mark?.CreatedAt, state == PollStates.Open && mark is null,
                PollRules.ResultsVisible(poll.Status, poll.ResultsMode, poll.OpensAt, poll.ClosesAt, poll.ResultsPublishedAt, now), dto).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading poll {PollId} for member {MemberId}", id, auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberPollDetail>("Failed to load the poll");
        }
    }

    public async Task<IApiResponse<object>> CastBallotAsync(string id, CastBallotRequest request, AuthData auth)
    {
        try
        {
            var outcome = await engine.CastAsync(id, auth.Id, request.Choices.Select(c => new BallotChoice(c.PositionId, c.AspirantId, c.Choice)).ToList(), DateTime.UtcNow);
            return outcome switch
            {
                CastOutcome.Recorded => new object().ToOkApiResponse("Your vote has been recorded"),
                CastOutcome.NotFound => ApiResponseExtensions.ToNotFoundApiResponse<object>("Poll not found"),
                CastOutcome.NotOpen => ApiResponseExtensions.ToBadRequestApiResponse<object>("Voting is not open for this poll."),
                CastOutcome.NotEligible => ApiResponseExtensions.ToForbiddenApiResponse<object>("This poll is not open to you."),
                CastOutcome.AlreadyVoted => ApiResponseExtensions.ToConflictApiResponse<object>("You have already voted in this poll."),
                CastOutcome.BallotChanged => ApiResponseExtensions.ToConflictApiResponse<object>("The ballot was updated while you were voting, or the system was busy. Nothing was recorded, so please try again."),
                CastOutcome.Incomplete => ApiResponseExtensions.ToBadRequestApiResponse<object>("Answer every position, or choose to abstain, before you submit."),
                _ => ApiResponseExtensions.ToBadRequestApiResponse<object>("Something in your ballot is not valid. Please start again."),
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error casting a ballot in poll {PollId} for member {MemberId}", id, auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("We could not record your vote. Please try again.");
        }
    }

    public async Task<IApiResponse<MemberPollResults>> GetResultsAsync(string id, AuthData auth)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            var (member, groups) = await WhoAsync(auth.Id);
            if (poll is null || member is null || !CanSee(poll, member, groups))
                return ApiResponseExtensions.ToNotFoundApiResponse<MemberPollResults>("Poll not found");
            var now = DateTime.UtcNow;
            if (!PollRules.ResultsVisible(poll.Status, poll.ResultsMode, poll.OpensAt, poll.ClosesAt, poll.ResultsPublishedAt, now))
                return ApiResponseExtensions.ToForbiddenApiResponse<MemberPollResults>("The results are not available yet.");

            var tally = await engine.TallyAsync(poll);
            return new MemberPollResults(poll.Id, poll.Title, PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, now), tally.Voted, tally.Eligible,
                tally.Positions.Select(p => new MemberPositionResult(p.PositionId, p.Title, p.IsYesNo, p.TotalVotes, p.Abstained,
                    p.Options.Select(o => new MemberOptionResult(o.AspirantId, o.Name, o.PhotoUrl, o.Votes, o.Percent, o.Leads)).ToList(), p.Yes, p.No, p.Passed, p.Tied)).ToList()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading results of poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberPollResults>("Failed to load the results");
        }
    }
}

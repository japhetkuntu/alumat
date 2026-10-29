using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Sdk.Polls;

public record PollOptionTally(string AspirantId, string Name, string? PhotoUrl, int Votes, double Percent, bool Leads);

/// <summary>The figures for one position. For a Yes/No question, Yes, No and Abstain are filled and Options holds the one aspirant.</summary>
public record PollPositionTally(
    string PositionId, string Title, bool IsYesNo, int TotalVotes, int Abstained,
    IReadOnlyList<PollOptionTally> Options, int Yes, int No, bool? Passed, bool Tied);

public record PollTally(int Voted, int Eligible, IReadOnlyList<PollPositionTally> Positions);

/// <summary>One answer on a ballot: a candidate, Yes/No on the one aspirant, or an abstention.</summary>
public record BallotChoice(string PositionId, string? AspirantId, string Choice);

public enum CastOutcome { Recorded, NotFound, NotOpen, NotEligible, AlreadyVoted, Incomplete, Invalid, BallotChanged }

/// <summary>
/// The parts of running an election that both portals need: who is eligible, what the figures are, and casting a ballot. The ballot is
/// secret: who voted goes in one table and what was chosen in another, with nothing to join them on.
/// </summary>
public class PollEngine(
    IAlumniPgRepository<Poll> pollRepo,
    IAlumniPgRepository<PollPosition> positionRepo,
    IAlumniPgRepository<PollAspirant> aspirantRepo,
    IAlumniPgRepository<PollParticipation> participationRepo,
    IAlumniPgRepository<PollVote> voteRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo)
{
    /// <summary>Ids of the active members who may vote in this poll.</summary>
    public async Task<List<string>> EligibleMemberIdsAsync(Poll poll)
    {
        if (!string.IsNullOrEmpty(poll.CommunityId))
        {
            var inGroup = await membershipRepo.GetQueryable(m => m.CommunityId == poll.CommunityId && m.Status == "Approved").Select(m => m.MemberId).ToListAsync();
            if (inGroup.Count == 0) return [];
            return await memberRepo.GetQueryable(m => m.Status == "Active" && inGroup.Contains(m.Id)).Select(m => m.Id).ToListAsync();
        }
        var years = poll.YearGroups;
        return years is { Count: > 0 }
            ? await memberRepo.GetQueryable(m => m.Status == "Active" && years.Contains(m.GraduationYear)).Select(m => m.Id).ToListAsync()
            : await memberRepo.GetQueryable(m => m.Status == "Active").Select(m => m.Id).ToListAsync();
    }

    public async Task<int> EligibleCountAsync(Poll poll)
    {
        if (!string.IsNullOrEmpty(poll.CommunityId)) return (await EligibleMemberIdsAsync(poll)).Count;
        var years = poll.YearGroups;
        return years is { Count: > 0 }
            ? await memberRepo.CountAsync(m => m.Status == "Active" && years.Contains(m.GraduationYear))
            : await memberRepo.CountAsync(m => m.Status == "Active");
    }

    /// <summary>Whether this member is active and in the poll's audience.</summary>
    public Task<bool> IsEligibleAsync(Poll poll, string memberId) => IsEligibleAsync(poll.YearGroups, poll.CommunityId, memberId);

    public async Task<bool> IsEligibleAsync(List<int>? yearGroups, string? communityId, string memberId)
    {
        var member = await memberRepo.GetQueryable(m => m.Id == memberId).AsNoTracking().Select(m => new { m.Status, m.GraduationYear }).FirstOrDefaultAsync();
        if (member is null || member.Status != "Active") return false;
        var inGroup = !string.IsNullOrEmpty(communityId)
            && await membershipRepo.CountAsync(m => m.CommunityId == communityId && m.MemberId == memberId && m.Status == "Approved") > 0;
        return PollRules.InAudience(yearGroups, communityId, member.GraduationYear, inGroup);
    }

    public async Task<PollTally> TallyAsync(Poll poll)
    {
        var positions = (await positionRepo.GetAllAsync(p => p.PollId == poll.Id)).OrderBy(p => p.SortOrder).ToList();
        var aspirants = (await aspirantRepo.GetAllAsync(a => a.PollId == poll.Id)).OrderBy(a => a.SortOrder).ToList();
        var counts = await voteRepo.GetQueryable(v => v.PollId == poll.Id)
            .GroupBy(v => new { v.PositionId, v.AspirantId, v.Choice })
            .Select(g => new { g.Key.PositionId, g.Key.AspirantId, g.Key.Choice, N = g.Count() }).ToListAsync();
        var voted = await participationRepo.CountAsync(p => p.PollId == poll.Id);
        var eligible = await EligibleCountAsync(poll);

        var result = new List<PollPositionTally>();
        foreach (var pos in positions)
        {
            var asp = aspirants.Where(a => a.PositionId == pos.Id).ToList();
            var mine = counts.Where(c => c.PositionId == pos.Id).ToList();
            var abstained = mine.Where(c => c.Choice == PollChoices.Abstain).Sum(c => c.N);

            if (PollRules.IsYesNo(asp.Count))
            {
                var yes = mine.Where(c => c.Choice == PollChoices.Yes).Sum(c => c.N);
                var no = mine.Where(c => c.Choice == PollChoices.No).Sum(c => c.N);
                var decided = yes + no;
                var a = asp[0];
                result.Add(new PollPositionTally(pos.Id, pos.Title, true, yes + no + abstained, abstained,
                    [new PollOptionTally(a.Id, a.Name, a.PhotoUrl, yes, decided == 0 ? 0 : Math.Round(100.0 * yes / decided, 1), yes > no)],
                    yes, no, decided == 0 ? null : yes > no, decided > 0 && yes == no));
                continue;
            }

            var total = mine.Where(c => c.Choice == PollChoices.Candidate).Sum(c => c.N);
            var tallies = asp.Select(a => (a, n: mine.Where(c => c.Choice == PollChoices.Candidate && c.AspirantId == a.Id).Sum(c => c.N))).ToList();
            var top = tallies.Count == 0 ? 0 : tallies.Max(t => t.n);
            var leaders = tallies.Count(t => t.n == top);
            var options = tallies.OrderByDescending(t => t.n).ThenBy(t => t.a.SortOrder)
                .Select(t => new PollOptionTally(t.a.Id, t.a.Name, t.a.PhotoUrl, t.n, total == 0 ? 0 : Math.Round(100.0 * t.n / total, 1), top > 0 && t.n == top && leaders == 1)).ToList();
            result.Add(new PollPositionTally(pos.Id, pos.Title, false, total + abstained, abstained, options, 0, 0, null, top > 0 && leaders > 1));
        }
        return new PollTally(voted, eligible, result);
    }

    /// <summary>
    /// Records a member's whole ballot, once, and never wrongly. All in one transaction, so it is recorded completely or not at all, and a
    /// member is never marked as having voted without their vote being there. What makes it safe when many people vote at the same moment, or
    /// an admin edits while a vote lands:
    ///  1. The "has voted" mark is behind a unique index, so two taps or two devices cannot both get a ballot in.
    ///  2. Each choice points at a real aspirant and position that the database will not let anyone delete while a vote is on it. If the
    ///     ballot was replaced a moment ago, the insert fails and the member is asked to try again.
    ///  3. Before committing, the poll is read again from the database. If it was closed, taken back to a draft or narrowed to exclude this
    ///     member in the meantime, nothing is recorded (judged at the time the member pressed submit, so one sent just before closing counts).
    /// </summary>
    public async Task<CastOutcome> CastAsync(string pollId, string memberId, IReadOnlyList<BallotChoice> choices, DateTime now)
    {
        var poll = await pollRepo.GetQueryable(p => p.Id == pollId).AsNoTracking().FirstOrDefaultAsync();
        if (poll is null || poll.Status != PollStatuses.Published || poll.ArchivedAt is not null) return CastOutcome.NotFound;
        if (PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, now) != PollStates.Open) return CastOutcome.NotOpen;
        if (!await IsEligibleAsync(poll, memberId)) return CastOutcome.NotEligible;
        if (await participationRepo.CountAsync(p => p.PollId == pollId && p.MemberId == memberId) > 0) return CastOutcome.AlreadyVoted;

        var positions = await positionRepo.GetQueryable(p => p.PollId == pollId).AsNoTracking().ToListAsync();
        var aspirants = await aspirantRepo.GetQueryable(a => a.PollId == pollId).AsNoTracking().ToListAsync();
        if (choices.Select(c => c.PositionId).Distinct().Count() != choices.Count) return CastOutcome.Invalid;
        if (positions.Any(p => choices.All(c => c.PositionId != p.Id))) return CastOutcome.Incomplete;

        var rows = new List<PollVote>();
        foreach (var c in choices)
        {
            if (positions.All(p => p.Id != c.PositionId)) return CastOutcome.Invalid;
            var options = aspirants.Where(a => a.PositionId == c.PositionId).ToList();
            var yesNo = PollRules.IsYesNo(options.Count);
            var valid = c.Choice switch
            {
                PollChoices.Abstain => true,
                PollChoices.Candidate => !yesNo && options.Any(o => o.Id == c.AspirantId),
                PollChoices.Yes or PollChoices.No => yesNo && options[0].Id == c.AspirantId,
                _ => false,
            };
            if (!valid) return CastOutcome.Invalid;
            rows.Add(new PollVote
            {
                InstitutionId = poll.InstitutionId, PollId = pollId, PositionId = c.PositionId,
                AspirantId = c.Choice == PollChoices.Abstain ? null : c.AspirantId, Choice = c.Choice,
                // The poll's own opening time, so the row's timestamp cannot be matched to when a member was marked as voted.
                CreatedAt = poll.OpensAt, CreatedBy = "ballot",
            });
        }

        var mark = new PollParticipation { InstitutionId = poll.InstitutionId, PollId = pollId, MemberId = memberId, CreatedBy = memberId };
        try
        {
            await participationRepo.RunInTransactionAsync(async () =>
            {
                await participationRepo.AddAsync(mark);
                await voteRepo.AddRangeAsync(rows);

                // Look again, straight from the database (not the copy read a moment ago), before committing.
                var live = await pollRepo.GetQueryable(p => p.Id == pollId).AsNoTracking()
                    .Select(p => new { p.Status, p.OpensAt, p.ClosesAt, p.ArchivedAt, p.YearGroups, p.CommunityId }).FirstOrDefaultAsync();
                if (live is null) throw new Refused(CastOutcome.NotFound);
                if (live.ArchivedAt is not null || PollRules.StateOf(live.Status, live.OpensAt, live.ClosesAt, now) != PollStates.Open) throw new Refused(CastOutcome.NotOpen);
                if (!await IsEligibleAsync(live.YearGroups, live.CommunityId, memberId)) throw new Refused(CastOutcome.NotEligible);
            });
            return CastOutcome.Recorded;
        }
        catch (Exception e)
        {
            // Whatever went wrong, the transaction has rolled back: no vote and no "has voted" mark. Forget the rows the context was still holding.
            participationRepo.Detach(mark);
            foreach (var r in rows) voteRepo.Detach(r);
            if (e is Refused refused) return refused.Outcome;
            if (DbErrors.IsUniqueViolation(e)) return CastOutcome.AlreadyVoted;
            // Either the ballot was replaced a moment ago, or the database stopped this insert and an admin's edit from waiting on each other.
            // Nothing is recorded in both cases, and trying again is safe.
            if (DbErrors.IsForeignKeyViolation(e) || DbErrors.IsDeadlock(e)) return CastOutcome.BallotChanged;
            throw;
        }
    }

    private sealed class Refused(CastOutcome outcome) : Exception { public CastOutcome Outcome { get; } = outcome; }
}

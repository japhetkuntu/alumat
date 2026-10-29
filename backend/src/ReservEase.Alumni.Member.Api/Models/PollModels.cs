namespace ReservEase.Alumni.Member.Api.Models;

public record BallotChoiceRequest(string PositionId, string? AspirantId, string Choice);
public class CastBallotRequest
{
    public List<BallotChoiceRequest> Choices { get; set; } = [];
}

public record MemberPollListItem(
    string Id, string Title, string? Description, string State, DateTime OpensAt, DateTime ClosesAt,
    string Audience, int PositionCount, bool HasVoted, bool ResultsVisible);

public record MemberAspirantDto(string Id, string Name, string? Details, string? PhotoUrl, string? MemberId);
public record MemberPositionDto(string Id, string Title, string? Description, bool IsYesNo, IReadOnlyList<MemberAspirantDto> Aspirants);

public record MemberPollDetail(
    string Id, string Title, string? Description, string State, DateTime OpensAt, DateTime ClosesAt, string Audience,
    bool HasVoted, DateTime? VotedAt, bool CanVote, bool ResultsVisible, IReadOnlyList<MemberPositionDto> Positions);

public record MemberOptionResult(string AspirantId, string Name, string? PhotoUrl, int Votes, double Percent, bool Leads);
public record MemberPositionResult(string PositionId, string Title, bool IsYesNo, int TotalVotes, int Abstained, IReadOnlyList<MemberOptionResult> Options, int Yes, int No, bool? Passed, bool Tied);
public record MemberPollResults(string PollId, string Title, string State, int Voted, int Eligible, IReadOnlyList<MemberPositionResult> Positions);

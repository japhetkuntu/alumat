namespace ReservEase.Alumni.Institution.Api.Models;

public class PollAspirantInput
{
    public string Name { get; set; } = "";
    public string? Details { get; set; }
    public string? PhotoUrl { get; set; }
    /// <summary>The aspirant's member profile, when they are a member. Optional.</summary>
    public string? MemberId { get; set; }
}

public class PollPositionInput
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<PollAspirantInput> Aspirants { get; set; } = [];
}

public class SavePollRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public List<int>? YearGroups { get; set; }
    public string? CommunityId { get; set; }
    public string ResultsMode { get; set; } = "AfterClose";
    /// <summary>The ballot. Leave null to keep it as it is (the only choice once voting has started, when it can no longer change).</summary>
    public List<PollPositionInput>? Positions { get; set; }
}

public record PollAspirantDto(string Id, string Name, string? Details, string? PhotoUrl, string? MemberId);
public record PollPositionDto(string Id, string Title, string? Description, bool IsYesNo, IReadOnlyList<PollAspirantDto> Aspirants);

public record PollListItem(
    string Id, string Title, string Status, string State, DateTime OpensAt, DateTime ClosesAt,
    List<int>? YearGroups, string? CommunityId, string? CommunityName, string ResultsMode, DateTime? ResultsPublishedAt,
    int PositionCount, int Voted, int Eligible, bool IsArchived, bool CanDelete);

public record PollDetail(
    string Id, string Title, string? Description, string Status, string State, DateTime OpensAt, DateTime ClosesAt,
    List<int>? YearGroups, string? CommunityId, string? CommunityName, string ResultsMode, DateTime? ResultsPublishedAt,
    DateTime? PublishedAt, bool IsArchived, int Voted, int Eligible,
    /// <summary>True once anyone has voted: the ballot and audience are then fixed, and only the closing time, the description and the results mode can change.</summary>
    bool Locked, bool CanDelete, IReadOnlyList<PollPositionDto> Positions);

public record PollOptionResult(string AspirantId, string Name, string? PhotoUrl, int Votes, double Percent, bool Leads);
public record PollPositionResult(string PositionId, string Title, bool IsYesNo, int TotalVotes, int Abstained, IReadOnlyList<PollOptionResult> Options, int Yes, int No, bool? Passed, bool Tied);

/// <summary>
/// The admin's view of the outcome. Turnout is always shown. The figures themselves appear only once voting has closed or the poll runs
/// live, so an admin cannot watch a vote while it is still being cast. Who voted for whom is not recorded anywhere.
/// </summary>
public record PollResultsDto(
    string PollId, string Title, string State, int Voted, int Eligible, bool FiguresAvailable, bool ShownToMembers,
    string ResultsMode, DateTime? ResultsPublishedAt, bool CanPublish, IReadOnlyList<PollPositionResult> Positions);

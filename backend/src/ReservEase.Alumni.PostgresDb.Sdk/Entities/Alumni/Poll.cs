using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// An election: a ballot of positions, each with aspirants, put to an audience between two times. Voting is secret. Who voted is
/// kept in <see cref="PollParticipation"/> and what was chosen in <see cref="PollVote"/>, and nothing links the two.
/// </summary>
public class Poll : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Draft (only admins see it) or Published (members in the audience see it). Open, closed and not-yet-open come from the dates; see <see cref="PollRules.StateOf"/>.</summary>
    public string Status { get; set; } = PollStatuses.Draft;

    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }

    /// <summary>Who may vote: empty and no group means every active member; otherwise members of these year groups, or of the group. A group takes precedence.</summary>
    public List<int>? YearGroups { get; set; }
    public string? CommunityId { get; set; }

    /// <summary>When members see the figures: Live (as votes come in), AfterClose (automatically once voting ends) or Manual (when an admin publishes them).</summary>
    public string ResultsMode { get; set; } = PollResultsModes.AfterClose;
    /// <summary>For Manual, when the admin published; set once and never changed. Null until then.</summary>
    public DateTime? ResultsPublishedAt { get; set; }

    public DateTime? PublishedAt { get; set; }
    /// <summary>When the "closes soon" reminder went to members who had not voted. Set once so nobody is reminded twice.</summary>
    public DateTime? ClosingReminderSentAt { get; set; }
    /// <summary>A finished poll put away so the list stays tidy. Its results stay.</summary>
    public DateTime? ArchivedAt { get; set; }
}

public class PollPosition : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string PollId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class PollAspirant : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string PollId { get; set; } = string.Empty;
    public string PositionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? PhotoUrl { get; set; }
    /// <summary>The member this aspirant is, when they are one, so voters can open their profile. Optional.</summary>
    public string? MemberId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>That a member has voted in a poll, so nobody votes twice. Deliberately holds no choice.</summary>
public class PollParticipation : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string PollId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

/// <summary>
/// One choice on one position. Deliberately holds no member and its CreatedAt is set to the poll's opening time, so neither the row nor
/// its timestamp can be matched to a <see cref="PollParticipation"/>.
/// </summary>
public class PollVote : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string PollId { get; set; } = string.Empty;
    public string PositionId { get; set; } = string.Empty;
    /// <summary>The aspirant chosen (or, on a Yes/No position, the aspirant being voted on). Null for an abstention.</summary>
    public string? AspirantId { get; set; }
    /// <summary>Candidate, Yes, No or Abstain.</summary>
    public string Choice { get; set; } = PollChoices.Candidate;
}

public static class PollStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
}

public static class PollResultsModes
{
    public const string Live = "Live";
    public const string AfterClose = "AfterClose";
    public const string Manual = "Manual";
    public static bool IsValid(string? v) => v is Live or AfterClose or Manual;
}

public static class PollChoices
{
    public const string Candidate = "Candidate";
    public const string Yes = "Yes";
    public const string No = "No";
    public const string Abstain = "Abstain";
}

public static class PollStates
{
    public const string Draft = "Draft";
    public const string Scheduled = "Scheduled";
    public const string Open = "Open";
    public const string Closed = "Closed";
}

/// <summary>The rules every portal shares, so what members see and what the server accepts can never disagree.</summary>
public static class PollRules
{
    /// <summary>Draft, Scheduled (published, not yet open), Open or Closed, worked out from the dates so nothing has to run for a poll to close.</summary>
    public static string StateOf(string status, DateTime opensAt, DateTime closesAt, DateTime now)
    {
        if (status != PollStatuses.Published) return PollStates.Draft;
        if (now < opensAt) return PollStates.Scheduled;
        return now < closesAt ? PollStates.Open : PollStates.Closed;
    }

    /// <summary>Whether members may see the figures now. An admin's own view is decided separately.</summary>
    public static bool ResultsVisible(string status, string resultsMode, DateTime opensAt, DateTime closesAt, DateTime? resultsPublishedAt, DateTime now)
    {
        var state = StateOf(status, opensAt, closesAt, now);
        if (state is PollStates.Draft or PollStates.Scheduled) return false;
        return resultsMode switch
        {
            PollResultsModes.Live => true,
            PollResultsModes.AfterClose => state == PollStates.Closed,
            PollResultsModes.Manual => resultsPublishedAt is not null,
            _ => false,
        };
    }

    /// <summary>Whether a member is in the poll's audience: a group's approved members, or members of the year groups, or everyone.</summary>
    public static bool InAudience(List<int>? yearGroups, string? communityId, int memberYear, bool isGroupMember)
    {
        if (!string.IsNullOrEmpty(communityId)) return isGroupMember;
        if (yearGroups is { Count: > 0 }) return yearGroups.Contains(memberYear);
        return true;
    }

    /// <summary>A position with exactly one aspirant is a Yes / No / Abstain question; otherwise it is a choice between them.</summary>
    public static bool IsYesNo(int aspirantCount) => aspirantCount == 1;
}

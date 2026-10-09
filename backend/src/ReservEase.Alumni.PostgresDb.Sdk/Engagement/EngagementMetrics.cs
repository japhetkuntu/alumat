namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

public record UpcomingEventInfo(string Id, string Title, DateTime StartDate, int Rsvps);

public record ActiveCampaignInfo(string Id, string Title, DateTime StartedAt, DateTime? LastUpdateAt, DateTime Deadline);

public record UncoveredCohort(int Year, int Members);

public record InactiveAmbassador(string StaffId, string Name, int DaysSinceActive);

public record WeeklyPoint(DateTime WeekStart, int NewMembers, int MeaningfulActions);

/// <summary>
/// Everything the health score, recommendations and checklist are worked out from: plain counts, read once.
/// The rules never touch the database, so they can be tested on numbers alone.
/// </summary>
public record EngagementMetrics
{
    public DateTime Now { get; init; }
    public int PeriodDays { get; init; }

    public int ActiveMembers { get; init; }
    public int PendingMembers { get; init; }
    /// <summary>Days the longest-waiting pending member has waited, or null when nobody is waiting.</summary>
    public int? OldestPendingDays { get; init; }

    public int NewMembers { get; init; }
    public int PreviousNewMembers { get; init; }
    public int JoinedLast7Days { get; init; }

    /// <summary>Active members who have signed in at least once and filled in at least one profile detail.</summary>
    public int Activated { get; init; }
    public int SignedInLast7Days { get; init; }
    public int SignedInLast30Days { get; init; }

    /// <summary>Distinct active members with at least one meaningful action in the period.</summary>
    public int Participants { get; init; }
    public int PreviousParticipants { get; init; }
    /// <summary>Of the previous period's participants, how many took a meaningful action again in this one.</summary>
    public int RetainedParticipants { get; init; }
    public int MeaningfulActionsLast7Days { get; init; }

    /// <summary>Members who joined over 30 days ago, have not signed in for 30 days and took no meaningful action in the period.</summary>
    public int Dormant { get; init; }

    /// <summary>Opportunities members have suggested that are waiting for an administrator.</summary>
    public int PendingOpportunities { get; init; }
    public int NewsPublishedThisWeek { get; init; }
    public int JobsPostedThisWeek { get; init; }

    public IReadOnlyList<UpcomingEventInfo> UpcomingEvents { get; init; } = [];
    public IReadOnlyList<ActiveCampaignInfo> ActiveCampaigns { get; init; } = [];

    /// <summary>Gross successful contribution volume. Not platform revenue; fees and processor charges are not netted off.</summary>
    public decimal ContributionVolume { get; init; }
    public decimal PreviousContributionVolume { get; init; }
    public int Contributors { get; init; }

    public IReadOnlyList<WeeklyPoint> Weekly { get; init; } = [];

    /// <summary>False for institutions without year groups (communities, or none recorded): the ambassador dimension is then left out of the score.</summary>
    public bool AmbassadorsAvailable { get; init; }
    /// <summary>Year groups with enough active members (see HealthScoreConfig.MinimumCohortMembers) for an ambassador to matter.</summary>
    public int CohortsWithMembers { get; init; }
    public int CohortsCovered { get; init; }
    public int AmbassadorCount { get; init; }
    public int ActiveAmbassadors { get; init; }
    public IReadOnlyList<UncoveredCohort> UncoveredCohorts { get; init; } = [];
    public IReadOnlyList<InactiveAmbassador> InactiveAmbassadors { get; init; } = [];
}

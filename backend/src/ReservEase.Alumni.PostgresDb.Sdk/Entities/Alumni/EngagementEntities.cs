namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public static class RecommendationStatuses
{
    public const string Open = "Open";
    public const string Completed = "Completed";
    public const string Dismissed = "Dismissed";
    public const string Snoozed = "Snoozed";
    /// <summary>The situation that raised it no longer holds, or its review date passed. Kept as history.</summary>
    public const string Expired = "Expired";
}

public static class RecommendationPriorities
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

/// <summary>
/// One day's reading of a community's health, kept so score changes can be shown and explained over time.
/// The factors that produced the score are stored with it: a score is only useful if it can be explained later,
/// when the rules or the data have moved on.
/// </summary>
public class CommunityHealthSnapshot : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    /// <summary>The UTC day this reading is for (time of day is always midnight). One reading per institution, day and period.</summary>
    public DateTime SnapshotDate { get; set; }
    public int PeriodDays { get; set; }
    /// <summary>Null when there was too little data to score fairly (see Classification "InsufficientData").</summary>
    public int? Score { get; set; }
    public string Classification { get; set; } = string.Empty;
    public int ActiveMembers { get; set; }
    public List<HealthFactor> Factors { get; set; } = [];
}

/// <summary>One scored dimension of a health reading, stored inside <see cref="CommunityHealthSnapshot.Factors"/>.</summary>
public class HealthFactor
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal ConfiguredWeight { get; set; }
    /// <summary>The share of the overall score this dimension carried, after unavailable dimensions were left out.</summary>
    public decimal EffectiveWeight { get; set; }
    public int? Score { get; set; }
    public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// A suggested action for an institution's administrators, raised by a rule from real community data.
/// (InstitutionId, DedupeKey) identifies the situation, so the same one is never raised twice while it is open,
/// snoozed, or inside its rule's cooldown after being completed or dismissed.
/// </summary>
public class EngagementRecommendation : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string DedupeKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    /// <summary>See <see cref="RecommendationPriorities"/>.</summary>
    public string Priority { get; set; } = RecommendationPriorities.Medium;
    public string ActionLabel { get; set; } = string.Empty;
    /// <summary>A path inside the institution portal, such as "/members".</summary>
    public string ActionUrl { get; set; } = string.Empty;
    /// <summary>See <see cref="RecommendationStatuses"/>.</summary>
    public string Status { get; set; } = RecommendationStatuses.Open;
    public DateTime? SnoozedUntil { get; set; }
    /// <summary>The review date: past it, an unresolved recommendation is retired rather than left to go stale.</summary>
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
}

/// <summary>An administrator ticking off a weekly checklist item. Items that complete themselves from data are not stored.</summary>
public class EngagementChecklistEntry : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    /// <summary>The Monday (UTC) of the week the item was completed for.</summary>
    public DateTime WeekStart { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public string CompletedById { get; set; } = string.Empty;
    public string CompletedByName { get; set; } = string.Empty;
}

public static class EngagementMessageKinds
{
    /// <summary>A reminder to an administrator who has not been active, saying what is waiting.</summary>
    public const string AdminReminder = "AdminReminder";
    /// <summary>A notice to the active administrators that a colleague has been away, suggesting delegation.</summary>
    public const string AdminEscalation = "AdminEscalation";
    /// <summary>A note to a member who has gone quiet, sent only when there is something real to show them.</summary>
    public const string MemberReengagement = "MemberReengagement";
}

/// <summary>
/// One engagement message the platform decided to send. Written when the message is queued, so it is both the audit trail
/// ("who was contacted, when, why") and the memory that frequency caps and cooldowns read. A person is never sent a second
/// message of the same kind until the cooldown has passed, whichever worker run is looking.
/// </summary>
public class EngagementMessage : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    /// <summary>"Member" or "Staff".</summary>
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    /// <summary>See <see cref="EngagementMessageKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Who or what it was about, for the audit trail (for example the colleague who had been away).</summary>
    public string? Subject { get; set; }
    public string Channel { get; set; } = "Email";
}

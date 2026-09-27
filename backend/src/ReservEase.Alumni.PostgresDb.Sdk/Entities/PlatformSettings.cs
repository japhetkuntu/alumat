namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// A single, platform-wide settings row — not tenant-scoped, applies to
/// every institution at once. Always read/written by the fixed id
/// <see cref="SingletonId"/> rather than a lookup, since there is exactly
/// one row and it's created on first read if missing (see wherever it's
/// fetched — no separate seed/migration data needed).
/// </summary>
public class PlatformSettings : BaseEntity
{
    public const string SingletonId = "platform-settings";

    /// <summary>
    /// When true, a fundraiser Campaign (never a membership-dues campaign —
    /// see Campaign.IsMembershipCampaign, always exempt) whose Deadline has
    /// passed stops accepting new contributions. Off by default, since this
    /// is a behavior change affecting every institution at once.
    /// </summary>
    public bool BlockOverdueCampaignPayments { get; set; }

    /// <summary>
    /// The onboarding goal the platform's Activation page tracks progress
    /// against — e.g. 20 activated institutions by a date. Null count hides
    /// the goal line entirely.
    /// </summary>
    public int? ActivationTargetCount { get; set; }
    public DateTime? ActivationTargetDate { get; set; }
    /// <summary>Interim checkpoints on the way to the target, e.g. "5 live by 16 Oct". Empty means none.</summary>
    public List<ActivationMilestone> ActivationMilestones { get; set; } = [];
}

/// <summary>One interim onboarding checkpoint. Either count may be omitted.</summary>
public class ActivationMilestone
{
    public DateTime Date { get; set; }
    public int? LiveTarget { get; set; }
    public int? ActivatedTarget { get; set; }
}

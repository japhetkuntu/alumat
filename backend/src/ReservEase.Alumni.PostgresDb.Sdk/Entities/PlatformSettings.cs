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
}

using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A sub-group inside an institution's alumni network (e.g. "Mining Engineering
/// Alumni") — created by institution admins; members request to join and, once
/// approved, see and participate in that community's own scoped content
/// (starting with Forum; other features follow the same CommunityId pattern).
/// The community's existence and basic info are publicly listable to every
/// member, but its actual content is members-only — see CommunityMembership.
/// </summary>
/// <summary>An external place a community talks (today a WhatsApp group). The community stays the source of structure and identity; this only records where its conversation happens.</summary>
public class CommunityChannel
{
    public const string WhatsApp = "WhatsApp";

    public string Type { get; set; } = WhatsApp;
    public string DisplayName { get; set; } = string.Empty;
    public string? InviteUrl { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public string? ConnectedBy { get; set; }

    /// <summary>Only an official WhatsApp group invite link is accepted, so a member is never sent somewhere unexpected.</summary>
    public static bool IsValidInviteUrl(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            && (u.Host == "chat.whatsapp.com" || u.Host == "wa.me" || u.Host == "whatsapp.com"));
}

public class Community : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>The institution chose to show this in full to people who are not signed in. Off means guests see only a short preview.</summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// What this group is called in this community: "Chapter", "Committee", "Department", "Ministry", "Team"… Free text the
    /// administrators choose, never a fixed list. Null reads as a plain "Group".
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>Optional external channels (e.g. the community's WhatsApp group). Null = none connected.</summary>
    public List<CommunityChannel>? ExternalChannels { get; set; }

    // ── Payout account ───────────────────────────────────────────────────────
    // The same arrangement as a year group (see Batch): a group can be paid into its own account instead of the institution's. What
    // the group's admin submits waits in PendingPayoutChanges until platform staff approve it; only approval writes the live fields.

    /// <summary>None (never set up), Pending (awaiting platform review), Approved (live), or Rejected (the institution's account is used).</summary>
    public string PayoutStatus { get; set; } = "None";

    /// <summary>True: this group settles into the institution's own account. False, once Approved: it has its own, below.</summary>
    public bool UseInstitutionAccount { get; set; }

    /// <summary>The live Paystack subaccount, only set once Approved. The platform fee percentage is always the institution's.</summary>
    public string? PaystackSubaccountCode { get; set; }
    public string? SettlementBankCode { get; set; }
    public string? SettlementBankName { get; set; }
    public string? SettlementAccountNumber { get; set; }
    public string? SettlementAccountName { get; set; }

    /// <summary>The latest submission, held until platform staff approve or reject it. Cleared by either.</summary>
    public BatchPayoutPendingChanges? PendingPayoutChanges { get; set; }
}

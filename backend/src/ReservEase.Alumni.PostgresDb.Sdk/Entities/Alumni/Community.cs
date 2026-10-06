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

    /// <summary>Optional external channels (e.g. the community's WhatsApp group). Null = none connected.</summary>
    public List<CommunityChannel>? ExternalChannels { get; set; }
}

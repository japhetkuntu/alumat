namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// A WhatsApp group an institution has connected to the AlumUnion Assistant. Held as a small list on the institution (no table of
/// its own): an institution has a handful of groups, and a group is looked up by its WhatsApp id across institutions, which a
/// scan of that short list per institution serves.
///
/// A group is never connected by the assistant noticing it. An administrator creates the link (which makes a one-time code),
/// someone adds the assistant to the group and posts <c>!link CODE</c>, the assistant records which group answered
/// (<see cref="WhatsAppGroupStatuses.Detected"/>), and the administrator confirms it in the portal. Only then does the group
/// become <see cref="WhatsAppGroupStatuses.Active"/> and answer commands.
/// </summary>
public class WhatsAppGroupLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The administrator's label, replaced by the group's own WhatsApp name once detected.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>WhatsApp's id for the group (ends "@g.us"). Null until the group has answered with the code.</summary>
    public string? GroupJid { get; set; }

    public string Status { get; set; } = WhatsAppGroupStatuses.AwaitingGroup;

    /// <summary>One-time code to post in the group. Cleared once used; expires after <see cref="CodeExpiresAt"/>.</summary>
    public string? Code { get; set; }
    public DateTime? CodeExpiresAt { get; set; }

    /// <summary>
    /// The group's own invitation link (https://chat.whatsapp.com/...). Optional, set by the administrator, and shown only to
    /// signed-in members of the community the group belongs to so they can join. Never used to read or manage the group.
    /// </summary>
    public string? InviteLink { get; set; }

    /// <summary>Set when the group belongs to one community (its leaders' group), null for an institution-wide group.</summary>
    public string? CommunityId { get; set; }

    /// <summary>Whether the assistant answers commands here. A connected group may be publish-only.</summary>
    public bool AssistantEnabled { get; set; } = true;

    /// <summary>Which commands it answers, without the "!" (events, campaigns, jobs, updates, community, help). Empty means all of them.</summary>
    public List<string> AllowedCommands { get; set; } = [];

    /// <summary>Whether administrators may post content from the portal to this group.</summary>
    public bool PublishingEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? LinkedAt { get; set; }
    public string? ConfirmedBy { get; set; }
}

public static class WhatsAppGroupStatuses
{
    /// <summary>A code exists; the assistant has not yet heard it in any group.</summary>
    public const string AwaitingGroup = "AwaitingGroup";
    /// <summary>A group posted the code; an administrator must confirm it is the right one.</summary>
    public const string Detected = "Detected";
    public const string Active = "Active";
}

public static class WhatsAppGroupCommands
{
    public static readonly IReadOnlyList<string> All = ["help", "events", "campaigns", "jobs", "updates", "community"];
}

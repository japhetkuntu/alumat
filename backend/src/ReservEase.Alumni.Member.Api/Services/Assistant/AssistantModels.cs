namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>
/// A community the assistant can talk about. <see cref="MemberId"/> is set when the sender's number belongs to a member of it;
/// <see cref="Verified"/> says whether that number was proved by a WhatsApp message (see Member.PhoneVerifiedAt). Only a verified
/// match is ever treated as the member: an unverified one is just a number somebody typed.
/// </summary>
public sealed record AssistantCommunity(
    string InstitutionId, string Slug, string Name, string PortalUrl, string? MemberId = null, bool Verified = false, string? MemberFirstName = null)
{
    public bool IsMember => Verified && MemberId is not null;
}

/// <summary>One line the assistant can show: a title, an optional second line, and the exact page it leads to.</summary>
public sealed record AssistantItem(string Title, string? Detail, string Url, string? Id = null);

public sealed record AssistantDues(bool CurrentYearPaid, bool HasArrears, int UnpaidCount, string Url);

/// <summary>What to send back, and to whom.</summary>
public sealed record AssistantReply(string To, string Text);

/// <summary>Short-lived memory of where a conversation is. Held in Redis for about half an hour, never in the database.</summary>
public sealed class AssistantState
{
    public string Stage { get; set; } = AssistantStages.Menu;
    public string? InstitutionId { get; set; }
    /// <summary>Institution ids behind the numbered list the user was last shown (community pick).</summary>
    public List<string> Choices { get; set; } = [];
    public int LinkFailures { get; set; }
    /// <summary>The menu option a guest asked for that needs a connected account (1 dues, 5 my community); answered once they connect.</summary>
    public int? Wanted { get; set; }
    public DateTime LinkFailureAt { get; set; }

    /// <summary>Event ids behind the numbered events list a member was last shown, for "rsvp 2". Bound to <see cref="EventsInstitutionId"/>.</summary>
    public List<string> EventIds { get; set; } = [];
    public string? EventsInstitutionId { get; set; }

    /// <summary>The guided flow in progress ("suggest" or "note"), the step it is on, and what has been typed so far.</summary>
    public string? Flow { get; set; }
    public int FlowStep { get; set; }
    public Dictionary<string, string> FlowData { get; set; } = [];

    /// <summary>The last message handled, so a redelivered webhook cannot submit the same thing twice.</summary>
    public string? LastMessageId { get; set; }
}

public static class AssistantStages
{
    public const string Menu = "menu";
    public const string PickCommunity = "pick";
    public const string AskCommunity = "ask";
    public const string Share = "share";
}

public interface IAssistantStateStore
{
    Task<AssistantState?> GetAsync(string phone);
    Task SetAsync(string phone, AssistantState state);
}

/// <summary>Who a WhatsApp number is, and which communities exist to talk about. All lookups cross institutions.</summary>
public interface IAssistantDirectory
{
    /// <summary>Active members with this (normalised) number, one per institution, with whether the number is verified for each.</summary>
    Task<IReadOnlyList<AssistantCommunity>> FindMemberCommunitiesAsync(string normalizedPhone, CancellationToken ct = default);

    Task<AssistantCommunity?> FindByIdAsync(string institutionId, CancellationToken ct = default);
    Task<AssistantCommunity?> FindBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Communities whose name or code matches, at most five, only those with the assistant switched on.</summary>
    Task<IReadOnlyList<AssistantCommunity>> SearchAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// The community information the assistant shows. "Public" members of this interface return only what an anonymous visitor to
/// the portal may see. The "Member" ones return what that verified member may see, by calling the same services the portal uses,
/// so the visibility rules are not copied.
/// </summary>
public interface IAssistantContent
{
    Task<IReadOnlyList<AssistantItem>> PublicCampaignsAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantItem>> PublicEventsAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantItem>> PublicJobsAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantItem>> PublicUpdatesAsync(AssistantCommunity c, CancellationToken ct = default);

    Task<IReadOnlyList<AssistantItem>> MemberCampaignsAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantItem>> MemberEventsAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantItem>> MemberJobsAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantItem>> MemberUpdatesAsync(AssistantCommunity c, CancellationToken ct = default);
    Task<AssistantDues?> MemberDuesAsync(AssistantCommunity c, CancellationToken ct = default);

    /// <summary>The member's own invitation link (their referral code, tagged as shared from WhatsApp). Null when it cannot be made.</summary>
    Task<string?> MemberInviteLinkAsync(AssistantCommunity c, CancellationToken ct = default);

    /// <summary>Whether the institution has this part of the product switched on at all.</summary>
    Task<bool> IsAvailableAsync(AssistantCommunity c, string feature, CancellationToken ct = default);
}

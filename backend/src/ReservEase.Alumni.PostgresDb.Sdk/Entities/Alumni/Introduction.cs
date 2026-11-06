namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One member asking another for an introduction, with a short note saying why. Nothing is shared and no message can be sent until
/// the other member accepts. Once accepted, the two can chat privately (see <see cref="IntroductionMessage"/>).
/// Names and photos are read from the live member rows, never copied here, so a changed name or a removed account is always current.
/// </summary>
public class Introduction : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string RequesterId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>Why the requester is asking, in their own words. Shown to the recipient only.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>One of <see cref="IntroductionStatuses"/>.</summary>
    public string Status { get; set; } = IntroductionStatuses.Pending;
    public DateTime? RespondedAt { get; set; }
    /// <summary>The member who ended an accepted conversation.</summary>
    public string? EndedById { get; set; }

    /// <summary>
    /// When anything last happened in this conversation (asked, answered, a message, ended, resumed). The lists are ordered and paged by this, so
    /// a connection that has not moved in years costs nothing to skip and the newest activity is always first.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastMessageAt { get; set; }
    public string? LastMessageSenderId { get; set; }
    /// <summary>A short, plain-text start of the latest message, for the conversation list.</summary>
    public string? LastMessagePreview { get; set; }
    /// <summary>When each side last opened the conversation. A message is unread for a side when it is newer than this.</summary>
    public DateTime? RequesterReadAt { get; set; }
    public DateTime? RecipientReadAt { get; set; }

    /// <summary>When the recipient was reminded, once, that this request is still waiting. Null until then. A request is never reminded twice.</summary>
    public DateTime? ReminderSentAt { get; set; }
}

public static class IntroductionStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    /// <summary>The recipient said no. The requester is not told: they simply see no reply.</summary>
    public const string Declined = "Declined";
    public const string Withdrawn = "Withdrawn";
    /// <summary>An accepted conversation that either side closed. No more messages.</summary>
    public const string Ended = "Ended";
}

/// <summary>A message in an accepted introduction. Plain text only.</summary>
public class IntroductionMessage : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string IntroductionId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>A member who does not want to hear from another member. Blocking works in both directions: neither can start or continue a conversation, and neither is told.</summary>
public class MemberBlock : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string BlockerId { get; set; } = string.Empty;
    public string BlockedId { get; set; } = string.Empty;
}

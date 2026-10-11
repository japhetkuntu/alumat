namespace ReservEase.Alumni.Member.Api.Services.Assistant;

public enum LinkStatus
{
    Linked,
    /// <summary>No such code, an expired one, or one already used. The three are deliberately indistinguishable to the sender.</summary>
    InvalidCode,
    /// <summary>The number already belongs to a different verified member of that institution.</summary>
    Conflict,
    /// <summary>The account behind the code is not an active member any more.</summary>
    NotAllowed,
    /// <summary>
    /// The code was right but the message came from a number that is not the one on the account. The member must approve that
    /// number in the signed-in portal. This is what stops a code a member was talked into forwarding from attaching a stranger's
    /// number to their account.
    /// </summary>
    NeedsApproval,
}

public sealed record LinkOutcome(LinkStatus Status, string? CommunityName = null, string? FirstName = null, string? InstitutionId = null, int AlsoLinked = 0)
{
    public static readonly LinkOutcome Invalid = new(LinkStatus.InvalidCode);
}

/// <summary>What the assistant needs from the account-linking service. Kept narrow so the engine can be tested without Redis or a database.</summary>
public interface IAssistantLinker
{
    /// <summary>Completes a challenge from a message the provider says came from <paramref name="senderPhone"/> (digits with country code).</summary>
    Task<LinkOutcome> CompleteAsync(string code, string senderPhone, CancellationToken ct = default);

    /// <summary>Removes the connection for this number across institutions. Returns how many accounts were disconnected.</summary>
    Task<int> UnlinkAsync(string senderPhone, CancellationToken ct = default);

    /// <summary>
    /// Turns WhatsApp alerts on or off for the accounts this number is connected to, leaving the connection itself in place
    /// ("STOP" must always work and must not log the member out of the assistant). Returns how many accounts were changed.
    /// </summary>
    Task<int> SetAlertsAsync(string senderPhone, bool on, CancellationToken ct = default);
}

public sealed record WhatsAppLinkChallenge(string Code, string? DeepLink, int ExpiresInSeconds, string? AssistantNumber);

public sealed record WhatsAppLinkStatus(
    bool Available, bool Connected, string? MaskedNumber, DateTime? VerifiedAt, string? AssistantNumber, bool NotificationsEnabled,
    /// <summary>A number that sent the member's code and is waiting for the member to approve it (masked). Null when nothing is waiting.</summary>
    string? PendingNumber = null);

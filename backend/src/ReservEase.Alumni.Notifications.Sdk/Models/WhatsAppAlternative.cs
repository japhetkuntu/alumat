namespace ReservEase.Alumni.Notifications.Sdk.Models;

/// <summary>How urgent a WhatsApp message is, which decides whether quiet hours and the daily limit apply to it.</summary>
public static class WhatsAppPriority
{
    /// <summary>A receipt or decision the member is waiting for. Always sent, at any hour, and not counted against the daily limit.</summary>
    public const string Transactional = "transactional";

    /// <summary>A reminder the member asked for (dues, event, pledge). Held back during quiet hours and counted against the daily limit.</summary>
    public const string Reminder = "reminder";

    /// <summary>News the member may like (a new job, a fundraiser, a digest). Same limits as a reminder.</summary>
    public const string Update = "update";
}

/// <summary>The WhatsApp version of an email: who it is for and the words to send. The link is appended by the sender.</summary>
public sealed class WhatsAppAlternative
{
    public required string InstitutionId { get; init; }
    public required string MemberId { get; init; }
    public required string Text { get; init; }
    public string? ActionUrl { get; init; }
    public string Priority { get; init; } = WhatsAppPriority.Reminder;
}

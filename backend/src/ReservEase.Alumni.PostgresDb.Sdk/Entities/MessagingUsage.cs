namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// How many notification messages an institution's members were reached by in one calendar month, by channel. Held as a small
/// month-keyed map on the institution (no table of its own): one entry a month, written by the notification worker.
/// Counts only: no numbers, no text, nothing about who.
/// </summary>
public class MessagingUsageMonth
{
    /// <summary>Alerts delivered by WhatsApp, each one an SMS that was not needed.</summary>
    public int WhatsAppDelivered { get; set; }

    /// <summary>Alerts sent by SMS (because WhatsApp was not eligible or did not deliver).</summary>
    public int SmsSent { get; set; }

    /// <summary>The part of <see cref="SmsSent"/> that went by SMS only because a WhatsApp attempt had failed.</summary>
    public int SmsAfterWhatsAppFailure { get; set; }
}

public static class MessagingUsageKeys
{
    /// <summary>"2026-10": the key for a month.</summary>
    public static string For(DateTime utc) => utc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}

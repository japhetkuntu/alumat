namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// Which automatic messages a community wants, and how often. The defaults reproduce exactly what every community
/// received before these were configurable, so existing communities see no change until an administrator edits them.
/// </summary>
public class AutomationSettings
{
    public const int MinReminderEveryDays = 7;
    public const int MaxReminderEveryDays = 90;
    public const int MinQuietDays = 14;
    public const int MaxQuietDays = 180;
    public const int MaxChaseAfterDays = 120;

    /// <summary>Remind members who owe membership dues.</summary>
    public bool DuesReminders { get; set; } = true;

    /// <summary>A member who has just been reminded is not reminded again for this many days.</summary>
    public int DuesReminderEveryDays { get; set; } = 21;

    /// <summary>Do not chase a member for dues until they have been a member this many days. 0 chases straight away.</summary>
    public int DuesChaseAfterDays { get; set; }

    /// <summary>The day-before reminder to people who RSVP'd.</summary>
    public bool EventReminders { get; set; } = true;

    /// <summary>Reminders about open pledges.</summary>
    public bool PledgeReminders { get; set; } = true;

    /// <summary>A friendly note to members who have gone quiet, with what they missed.</summary>
    public bool ReengageQuietMembers { get; set; } = true;

    /// <summary>A member counts as quiet after this many days without signing in or taking part.</summary>
    public int QuietDays { get; set; } = 30;

    /// <summary>Clamps every number into its allowed range, so a hand-edited or old value can never make the schedule absurd.</summary>
    public AutomationSettings Normalised() => new()
    {
        DuesReminders = DuesReminders,
        DuesReminderEveryDays = Math.Clamp(DuesReminderEveryDays, MinReminderEveryDays, MaxReminderEveryDays),
        DuesChaseAfterDays = Math.Clamp(DuesChaseAfterDays, 0, MaxChaseAfterDays),
        EventReminders = EventReminders,
        PledgeReminders = PledgeReminders,
        ReengageQuietMembers = ReengageQuietMembers,
        QuietDays = Math.Clamp(QuietDays, MinQuietDays, MaxQuietDays),
    };
}

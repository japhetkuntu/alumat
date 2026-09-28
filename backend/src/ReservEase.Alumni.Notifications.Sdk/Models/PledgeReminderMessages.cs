namespace ReservEase.Alumni.Notifications.Sdk.Models;

/// <summary>
/// The wording of pledge reminders, shared by the scheduled reminder job and an admin's "send a reminder now" so the
/// two never say different things. Deliberately gentle: a pledge is an intention, not a debt.
/// </summary>
public static class PledgeReminderMessages
{
    public const string Upcoming = "Upcoming";
    public const string Due = "Due";
    public const string Overdue = "Overdue";

    public static (string Title, string Body, string ActionLabel) Build(string stage, string campaignTitle, decimal outstanding, DateTime dueDateUtc)
    {
        var amount = $"GHS {outstanding:N2}";
        var due = dueDateUtc.ToString("MMMM d, yyyy");
        return stage switch
        {
            Upcoming => ("Your pledge is coming up",
                $"You pledged {amount} to \"{campaignTitle}\", planned for {due}. Whenever you're ready, you can give in a minute.", "Give now"),
            Due => ("Today's the day for your pledge",
                $"You pledged {amount} to \"{campaignTitle}\" for today. Thank you for planning to give. You can complete it now.", "Complete my pledge"),
            _ => ("A gentle reminder about your pledge",
                $"You pledged {amount} to \"{campaignTitle}\" by {due}. If you can still give, it would mean a lot. If plans have changed, you can cancel the pledge from your account.", "Complete my pledge"),
        };
    }
}

using System.Net;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// The wording of the automated engagement emails. Anything an administrator or member typed (an event's title, an institution's name)
/// is HTML-encoded here, because it ends up inside an email template.
/// </summary>
public static class EngagementMessageText
{
    public static (string Title, string Body) QuietMember(string brand, IReadOnlyList<string> items)
    {
        var shown = items.Take(3).Select(WebUtility.HtmlEncode);
        return ($"Something is happening at {WebUtility.HtmlEncode(brand)}",
            $"It has been a while. Here is what is happening at {WebUtility.HtmlEncode(brand)}: {string.Join(". ", shown)}. You can switch these notes off any time in your profile settings.");
    }

    public static (string Title, string Body) AdminReminder(string brand, int? daysAway, int suggestions, int pendingMembers)
    {
        var parts = new[]
        {
            suggestions > 0 ? $"{suggestions} suggested {(suggestions == 1 ? "action" : "actions")}" : null,
            pendingMembers > 0 ? $"{pendingMembers} {(pendingMembers == 1 ? "member" : "members")} waiting for approval" : null,
        }.Where(x => x is not null);
        var waiting = string.Join(" and ", parts);
        return ($"{WebUtility.HtmlEncode(brand)}: your community has things waiting",
            $"It has been {(daysAway is { } d ? $"{d} days" : "a while")} since you last visited. There {(suggestions + pendingMembers == 1 ? "is" : "are")} {waiting}. A few minutes now keeps new members from drifting away.");
    }

    public static (string Title, string Body) AdminEscalation(string brand, string colleague, int? daysAway)
    {
        var who = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(colleague) ? "A fellow administrator" : colleague);
        return ($"{WebUtility.HtmlEncode(brand)}: {who} has been away",
            $"{who} has not been active for {(daysAway is { } d ? $"{d} days" : "some time")}, and there is work waiting. You may want to delegate it or appoint a year-group ambassador. Nothing has been changed on their account.");
    }
}

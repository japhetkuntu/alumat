using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

public record ChecklistItem(string Key, string Title, string Detail, string ActionUrl, bool Done, bool AutomaticallyDone);

/// <summary>
/// The week's checklist, fitted to this community: an item only appears if it can apply (no event, no "promote the event"; a
/// switched-off feature, no item for it). Some items complete themselves from real activity; the rest an administrator ticks.
/// </summary>
public static class EngagementChecklist
{
    public static DateTime WeekStart(DateTime now)
    {
        var date = now.Date;
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
        return DateTime.SpecifyKind(date.AddDays(-offset), DateTimeKind.Utc);
    }

    public static IReadOnlyList<ChecklistItem> Build(EngagementMetrics m, ISet<string> disabledFeatures, ISet<string> ticked)
    {
        bool On(string f) => !disabledFeatures.Contains(f);
        var items = new List<ChecklistItem>();

        void Add(string key, string title, string detail, string url, bool auto = false) =>
            items.Add(new ChecklistItem(key, title, detail, url, auto || ticked.Contains(key), auto));

        Add("review-pending", "Review pending member requests",
            m.PendingMembers == 0 ? "Nobody is waiting." : $"{m.PendingMembers} waiting.", "/members", auto: m.PendingMembers == 0);
        if (m.JoinedLast7Days > 0)
            Add("welcome-new-members", "Welcome new members", $"{m.JoinedLast7Days} joined in the last 7 days.", "/members");
        if (On(InstitutionFeatures.News))
            Add("publish-update", "Publish a useful community update", m.NewsPublishedThisWeek > 0 ? "Published this week." : "Nothing published this week.", "/news", auto: m.NewsPublishedThisWeek > 0);
        if (On(InstitutionFeatures.Jobs))
            Add("share-opportunity", "Share an opportunity", m.JobsPostedThisWeek > 0 ? "Posted this week." : "Nothing posted this week.", "/jobs", auto: m.JobsPostedThisWeek > 0);
        if (On(InstitutionFeatures.Events) && m.UpcomingEvents.Count > 0)
            Add("promote-event", "Promote an upcoming event", $"{m.UpcomingEvents.Count} coming up.", $"/events/{m.UpcomingEvents[0].Id}");
        if (On(InstitutionFeatures.Spotlights))
            Add("highlight-member", "Highlight a member's achievement", "Celebrate someone's win.", "/spotlights");
        Add("check-health", "Review community health", "Read this page's score and its factors.", "/engagement");
        return items;
    }
}

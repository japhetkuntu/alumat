using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

public record RuleOptions
{
    public int MinimumMembers { get; init; } = 5;
    public int PendingOverdueDays { get; init; } = 3;
    public int QuietDays { get; init; } = 7;
    public int EventLeadDays { get; init; } = 14;
    /// <summary>An event with fewer sign-ups than this share of active members is worth promoting.</summary>
    public decimal EventLowSignUpShare { get; init; } = 0.10m;
    public int CampaignStaleDays { get; init; } = 14;
    public decimal DormantShare { get; init; } = 0.20m;
    public int DormantMinimum { get; init; } = 10;
    /// <summary>Participation must fall below this share of the previous period's to count as declining.</summary>
    public decimal DeclineBelow { get; init; } = 0.70m;
    public int DeclineMinimumPrevious { get; init; } = 10;
    /// <summary>An ambassador unseen this long is flagged.</summary>
    public int AmbassadorInactiveDays { get; init; } = 21;
}

/// <summary>One suggestion a rule wants raised. Cooldown is how long a finished or dismissed one blocks the same situation from returning.</summary>
public record RecommendationCandidate(
    string RuleId, string DedupeKey, string Title, string Explanation, string Priority,
    string ActionLabel, string ActionUrl, DateTime ExpiresAt, TimeSpan Cooldown);

/// <summary>
/// The rules that turn numbers into things an administrator can do. Each rule is a small, separate check that reads only
/// the metrics, names the action and its link, and says when to stop suggesting it. A rule that is not true right now
/// returns nothing. Rules never invent anything: a campaign is only mentioned if it exists, an event only if it is coming.
/// </summary>
public static class RecommendationRules
{
    public static IReadOnlyList<RecommendationCandidate> Evaluate(EngagementMetrics m, ISet<string> disabledFeatures, RuleOptions? options = null)
    {
        options ??= new RuleOptions();
        var list = new List<RecommendationCandidate>();
        var week = IsoWeekKey(m.Now);
        bool On(string feature) => !disabledFeatures.Contains(feature);

        // Too little data for any of the percentage-based rules to be fair. Counts of waiting people still apply.
        var enough = m.ActiveMembers >= options.MinimumMembers;

        if (m.PendingMembers > 0 && m.OldestPendingDays >= options.PendingOverdueDays)
            list.Add(new("pending-members", $"pending-members:{week}",
                $"Review {m.PendingMembers} {(m.PendingMembers == 1 ? "member waiting" : "members waiting")} for approval",
                $"The longest has waited {m.OldestPendingDays} days. People who wait too long often do not come back.",
                RecommendationPriorities.High, "Review requests", "/members", m.Now.AddDays(7), TimeSpan.FromDays(3)));

        if (m.PendingOpportunities > 0 && On(InstitutionFeatures.Jobs))
            list.Add(new("review-opportunities", $"review-opportunities:{week}",
                $"Review {m.PendingOpportunities} {(m.PendingOpportunities == 1 ? "opportunity" : "opportunities")} suggested by members",
                "Members only see a suggestion once you approve it. Reviewing promptly keeps people contributing.",
                RecommendationPriorities.Medium, "Review suggestions", "/jobs?status=Pending", m.Now.AddDays(7), TimeSpan.FromDays(3)));

        if (m.JoinedLast7Days > 0)
            list.Add(new("welcome-new-members", $"welcome-new-members:{week}",
                $"Welcome {m.JoinedLast7Days} {(m.JoinedLast7Days == 1 ? "new member" : "new members")}",
                "A personal welcome in the first week is what turns a sign-up into a member who returns.",
                RecommendationPriorities.Medium, "See new members", "/members", m.Now.AddDays(7), TimeSpan.FromDays(6)));

        if (enough && m.MeaningfulActionsLast7Days == 0 && (On(InstitutionFeatures.News) || On(InstitutionFeatures.Events)))
            list.Add(new("quiet-community", $"quiet-community:{week}",
                $"Nothing meaningful has happened in {options.QuietDays} days",
                "No discussions, event sign-ups, gifts or class notes this week. A useful update or an upcoming event gives members a reason to come back.",
                RecommendationPriorities.High, On(InstitutionFeatures.News) ? "Publish an update" : "Plan an event",
                On(InstitutionFeatures.News) ? "/news" : "/events", m.Now.AddDays(7), TimeSpan.FromDays(6)));

        if (enough && On(InstitutionFeatures.Events))
            foreach (var e in m.UpcomingEvents.Where(e => e.StartDate <= m.Now.AddDays(options.EventLeadDays)))
            {
                if (e.Rsvps >= Math.Ceiling(m.ActiveMembers * options.EventLowSignUpShare)) continue;
                var days = Math.Max(0, (int)Math.Ceiling((e.StartDate - m.Now).TotalDays));
                list.Add(new("promote-event", $"promote-event:{e.Id}",
                    $"Promote \"{e.Title}\"",
                    $"It is in {days} {(days == 1 ? "day" : "days")} with {e.Rsvps} {(e.Rsvps == 1 ? "sign-up" : "sign-ups")} so far. Sharing it into your groups usually brings the most sign-ups.",
                    RecommendationPriorities.Medium, "Open event", $"/events/{e.Id}", e.StartDate, TimeSpan.FromDays(4)));
            }

        if (On(InstitutionFeatures.Contributions))
            foreach (var c in m.ActiveCampaigns)
            {
                var reference = c.LastUpdateAt ?? c.StartedAt;
                if ((m.Now - reference).TotalDays < options.CampaignStaleDays) continue;
                list.Add(new("campaign-update", $"campaign-update:{c.Id}",
                    $"Update members on \"{c.Title}\"",
                    c.LastUpdateAt is null ? "No progress update has been posted yet." : $"The last update was {(int)(m.Now - c.LastUpdateAt.Value).TotalDays} days ago.",
                    RecommendationPriorities.Medium, "Open fundraiser", $"/campaigns/{c.Id}", c.Deadline > m.Now ? c.Deadline : m.Now.AddDays(7), TimeSpan.FromDays(7)));
            }

        if (enough && m.Dormant >= options.DormantMinimum && (decimal)m.Dormant / m.ActiveMembers >= options.DormantShare)
            list.Add(new("dormant-members", $"dormant-members:{MonthKey(m.Now)}",
                $"{m.Dormant} members have gone quiet",
                $"They joined over a month ago and have neither signed in nor taken part for 30 days. A short, specific message (an event, a new opportunity) works better than a general reminder.",
                RecommendationPriorities.Medium, "Message members", "/broadcast", m.Now.AddDays(14), TimeSpan.FromDays(21)));

        if (enough && m.PreviousParticipants >= options.DeclineMinimumPrevious && m.Participants < m.PreviousParticipants * options.DeclineBelow)
            list.Add(new("declining-participation", $"declining-participation:{week}",
                "Participation is down on the previous period",
                $"{m.Participants} members took part in the last {m.PeriodDays} days, against {m.PreviousParticipants} in the {m.PeriodDays} days before.",
                RecommendationPriorities.High, "See analytics", "/analytics", m.Now.AddDays(14), TimeSpan.FromDays(10)));

        if (m.AmbassadorsAvailable)
        {
            var uncovered = m.UncoveredCohorts.OrderByDescending(u => u.Members).ToList();
            if (uncovered.Count > 0)
            {
                var names = string.Join(", ", uncovered.Take(3).Select(u => $"{u.Year} ({u.Members})"));
                list.Add(new("unassigned-cohort", $"unassigned-cohort:{MonthKey(m.Now)}",
                    $"{uncovered.Count} {(uncovered.Count == 1 ? "year group has" : "year groups have")} no ambassador",
                    $"Largest without one: {names}. An ambassador from the year group welcomes new members and keeps their class connected, so it does not all fall on you.",
                    RecommendationPriorities.Medium, "Appoint an ambassador", "/staff", m.Now.AddDays(30), TimeSpan.FromDays(14)));
            }
            foreach (var a in m.InactiveAmbassadors)
                list.Add(new("inactive-ambassador", $"inactive-ambassador:{a.StaffId}:{MonthKey(m.Now)}",
                    $"{a.Name} has not been active for {a.DaysSinceActive} days",
                    "Their year group may be going unattended. Check in with them, or appoint someone else for it. Nothing changes automatically.",
                    RecommendationPriorities.Medium, "Review ambassadors", "/staff", m.Now.AddDays(21), TimeSpan.FromDays(14)));
        }

        return list;
    }

    public static string IsoWeekKey(DateTime d) => $"{System.Globalization.ISOWeek.GetYear(d)}-W{System.Globalization.ISOWeek.GetWeekOfYear(d):00}";
    private static string MonthKey(DateTime d) => $"{d.Year}-{d.Month:00}";
}

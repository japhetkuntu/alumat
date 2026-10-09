using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

/// <summary>
/// The limits on automated engagement messages, in one place. Defaults are deliberately conservative; the point of the
/// system is that a community feels looked after, not pestered.
/// </summary>
public record AutomationLimits
{
    /// <summary>Messages are only queued between these UTC hours (inclusive start, exclusive end). Ghana is on UTC, so this is daytime there.</summary>
    public int SendWindowStartHour { get; init; } = 8;
    public int SendWindowEndHour { get; init; } = 18;

    public int AdminInactiveDays { get; init; } = 14;
    public int AdminReminderCooldownDays { get; init; } = 14;
    public int AdminAwayForEscalationDays { get; init; } = 30;
    public int AdminEscalationCooldownDays { get; init; } = 30;

    public int MemberQuietDays { get; init; } = 30;
    /// <summary>The same member hears from us this way at most once in this many days.</summary>
    public int MemberReengagementCooldownDays { get; init; } = 30;
    /// <summary>And never more than one engagement message of any kind in this many days.</summary>
    public int MemberAnyMessageCooldownDays { get; init; } = 7;
    /// <summary>The most members one institution's run may contact, so a long-neglected community is warmed up gradually.</summary>
    public int MaxMemberMessagesPerRun { get; init; } = 100;
}

public record StaffState(string StaffId, string Name, string Email, DateTime? LastActiveAt);

public record AdminMessagePlan(string RecipientId, string RecipientName, string RecipientEmail, string Kind, string? Subject, int? DaysAway);

public static class AutomationPolicy
{
    public static bool InSendWindow(DateTime nowUtc, AutomationLimits? limits = null)
    {
        limits ??= new AutomationLimits();
        return nowUtc.Hour >= limits.SendWindowStartHour && nowUtc.Hour < limits.SendWindowEndHour;
    }

    private static bool CooledDown(IReadOnlyDictionary<(string Id, string Kind, string? Subject), DateTime> sent, string id, string kind, string? subject, int days, DateTime now) =>
        !sent.TryGetValue((id, kind, subject), out var at) || at <= now.AddDays(-days);

    /// <summary>
    /// Reminds an administrator who has been away when there is real work waiting, and, once a colleague has been away a long
    /// time, tells the administrators who are still active so they can delegate. Nothing is transferred or changed.
    /// </summary>
    public static IReadOnlyList<AdminMessagePlan> PlanAdminMessages(
        IReadOnlyList<StaffState> superAdmins, int openRecommendations, int pendingMembers, DateTime now,
        IReadOnlyDictionary<(string Id, string Kind, string? Subject), DateTime> sent, AutomationLimits? limits = null)
    {
        limits ??= new AutomationLimits();
        var plans = new List<AdminMessagePlan>();
        if (openRecommendations + pendingMembers == 0) return plans; // nothing is waiting, so there is nothing to remind anyone about

        int DaysAway(StaffState s) => s.LastActiveAt is { } at ? Math.Max(0, (int)(now - at).TotalDays) : int.MaxValue;
        bool Away(StaffState s, int days) => s.LastActiveAt is null || s.LastActiveAt <= now.AddDays(-days);

        var usable = superAdmins.Where(s => !string.IsNullOrWhiteSpace(s.Email)).ToList();
        var inactive = usable.Where(s => Away(s, limits.AdminInactiveDays)).ToList();
        var active = usable.Where(s => !Away(s, limits.AdminInactiveDays)).ToList();

        foreach (var a in inactive.Where(a => CooledDown(sent, a.StaffId, EngagementMessageKinds.AdminReminder, null, limits.AdminReminderCooldownDays, now)))
            plans.Add(new AdminMessagePlan(a.StaffId, a.Name, a.Email, EngagementMessageKinds.AdminReminder, null, DaysAway(a) == int.MaxValue ? null : DaysAway(a)));

        // A colleague who has been away a long time, told to the administrators still around. With nobody active there is no one
        // inside the institution to tell, so nothing is sent here.
        foreach (var away in usable.Where(s => Away(s, limits.AdminAwayForEscalationDays)))
            foreach (var recipient in active.Where(r => r.StaffId != away.StaffId
                         && CooledDown(sent, r.StaffId, EngagementMessageKinds.AdminEscalation, away.StaffId, limits.AdminEscalationCooldownDays, now)))
                plans.Add(new AdminMessagePlan(recipient.StaffId, recipient.Name, recipient.Email, EngagementMessageKinds.AdminEscalation, away.StaffId, DaysAway(away) == int.MaxValue ? null : DaysAway(away)));

        return plans;
    }

    /// <summary>One member's eligibility for a re-engagement note. Every condition must hold.</summary>
    public static bool ShouldReengage(
        bool optedOut, bool institutionEmailOn, bool hasEmail, DateTime joinedAt, DateTime? lastLoginAt, bool tookPartRecently,
        DateTime? lastReengagementAt, DateTime? lastAnyMessageAt, int contentItems, DateTime now, AutomationLimits? limits = null)
    {
        limits ??= new AutomationLimits();
        if (optedOut || !institutionEmailOn || !hasEmail) return false;
        if (contentItems <= 0) return false; // never an empty "we miss you"
        if (joinedAt > now.AddDays(-limits.MemberQuietDays)) return false;                              // new members are still being welcomed
        if (lastLoginAt is { } login && login > now.AddDays(-limits.MemberQuietDays)) return false;     // they are around
        if (tookPartRecently) return false;
        if (lastReengagementAt is { } r && r > now.AddDays(-limits.MemberReengagementCooldownDays)) return false;
        if (lastAnyMessageAt is { } m && m > now.AddDays(-limits.MemberAnyMessageCooldownDays)) return false;
        return true;
    }
}

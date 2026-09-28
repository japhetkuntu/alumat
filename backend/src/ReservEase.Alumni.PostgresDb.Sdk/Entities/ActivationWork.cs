namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// A time-boxed goal the platform team is working towards, e.g. "20 live institutions by 31 December". One owner (a
/// platform admin) is accountable for it. Not tenant-scoped. Progress is measured from real platform data (see
/// <see cref="TargetMetrics"/>), never typed in, except for the "Custom" metric. A target never repeats: once it ends
/// (achieved, missed or cancelled) the team creates a new one.
/// </summary>
public class ActivationTarget : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>One of <see cref="TargetMetrics"/>.</summary>
    public string Metric { get; set; } = TargetMetrics.LiveInstitutions;
    public decimal GoalValue { get; set; }
    /// <summary>What the metric read when the target was created, so pace is measured from where the team started.</summary>
    public decimal BaselineValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime DueDate { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    /// <summary>One of <see cref="TargetStatuses"/>.</summary>
    public string Status { get; set; } = TargetStatuses.Active;
    /// <summary>Only used by the Custom metric, updated by hand.</summary>
    public decimal? ManualValue { get; set; }
    /// <summary>The metric's value at the moment the target ended, kept so the result never changes afterwards.</summary>
    public decimal? FinalValue { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public static class TargetMetrics
{
    /// <summary>Institutions that are live right now.</summary>
    public const string LiveInstitutions = "LiveInstitutions";
    /// <summary>Live institutions that have reached activation right now.</summary>
    public const string ActivatedInstitutions = "ActivatedInstitutions";
    /// <summary>Active members across every institution right now.</summary>
    public const string TotalMembers = "TotalMembers";
    /// <summary>Onboarding requests received since the target started.</summary>
    public const string OnboardingLeads = "OnboardingLeads";
    /// <summary>Successful payments (contributions, store, services) collected since the target started.</summary>
    public const string PaymentVolume = "PaymentVolume";
    /// <summary>Measured by hand.</summary>
    public const string Custom = "Custom";

    public static readonly string[] All = [LiveInstitutions, ActivatedInstitutions, TotalMembers, OnboardingLeads, PaymentVolume, Custom];
    /// <summary>Flow metrics count what happened since the start; the others read a current level.</summary>
    public static bool IsFlow(string metric) => metric is OnboardingLeads or PaymentVolume;
}

public static class TargetStatuses
{
    public const string Active = "Active";
    public const string Achieved = "Achieved";
    public const string Missed = "Missed";
    public const string Cancelled = "Cancelled";
}

/// <summary>A piece of work under a <see cref="ActivationTarget"/>, with one assignee. Optionally about one institution or onboarding lead.</summary>
public class ActivationTask : BaseEntity
{
    public string TargetId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssigneeId { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty;
    /// <summary>A date; the task is overdue once that whole day has passed.</summary>
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = TaskPriorities.Normal;
    public string Status { get; set; } = TaskStatuses.Todo;
    public string? BlockedReason { get; set; }
    public string? InstitutionId { get; set; }
    public string? InstitutionName { get; set; }
    public string? LeadId { get; set; }
    public string? LeadName { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }

    // Reminder bookkeeping, so each alert goes out once (cleared when the due date changes or the task is reassigned).
    public DateTime? DueSoonNotifiedAt { get; set; }
    public DateTime? OverdueNotifiedAt { get; set; }
    public DateTime? LastReminderAt { get; set; }
}

public static class TaskStatuses
{
    public const string Todo = "Todo";
    public const string InProgress = "InProgress";
    public const string Blocked = "Blocked";
    public const string Done = "Done";
    public static readonly string[] All = [Todo, InProgress, Blocked, Done];
}

public static class TaskPriorities
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public static readonly string[] All = [Low, Normal, High];
}

/// <summary>A short note left on a task: who wrote it and when. Not a chat thread.</summary>
public class ActivationTaskNote : BaseEntity
{
    public string TaskId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public readonly record struct TargetProgressResult(decimal Baseline, decimal Expected, int Percent, string Health);

/// <summary>
/// How far along a target is and whether it is keeping pace. A pure function of the target, its current reading and
/// today's date, so it can be tested without a database. Health is Achieved, OnTrack, Behind, Missed or Cancelled.
/// </summary>
public static class ActivationTargetMath
{
    public static TargetProgressResult Compute(ActivationTarget t, decimal current, DateTime today)
    {
        // A flow metric counts from zero at the start; a level metric is measured from where the team started.
        var baseline = TargetMetrics.IsFlow(t.Metric) ? 0m : t.BaselineValue;
        var span = t.GoalValue - baseline;
        var percent = span <= 0
            ? (current >= t.GoalValue ? 100 : 0)
            : (int)Math.Clamp(Math.Round((current - baseline) / span * 100), 0, 100);

        // Where the target should be by today if progress were steady from start to finish.
        var totalDays = Math.Max(1.0, (t.DueDate.Date - t.StartDate.Date).TotalDays);
        var fraction = Math.Clamp((today.Date - t.StartDate.Date).TotalDays / totalDays, 0.0, 1.0);
        var expected = baseline + span * (decimal)fraction;

        var health = t.Status switch
        {
            TargetStatuses.Cancelled => "Cancelled",
            TargetStatuses.Achieved => "Achieved",
            TargetStatuses.Missed => "Missed",
            _ => current >= t.GoalValue ? "Achieved"
               // A 5% grace so a small wobble around the pace line doesn't flip the flag.
               : current + span * 0.05m >= expected ? "OnTrack" : "Behind",
        };
        return new TargetProgressResult(baseline, Math.Round(expected, 2), percent, health);
    }
}

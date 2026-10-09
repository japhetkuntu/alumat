using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

/// <summary>Weights, targets and cut-offs for the score. Starting assumptions, not benchmarks: change them here, not in the logic.</summary>
public record HealthScoreConfig
{
    public decimal ActivationWeight { get; init; } = 25;
    public decimal ParticipationWeight { get; init; } = 30;
    public decimal RetentionWeight { get; init; } = 20;
    public decimal AmbassadorWeight { get; init; } = 15;
    public decimal ResponsivenessWeight { get; init; } = 10;

    /// <summary>The share of members activated that earns full marks.</summary>
    public decimal ActivationTarget { get; init; } = 0.70m;
    /// <summary>The share of members taking a meaningful action in the period that earns full marks. Low on purpose: most communities are mostly quiet.</summary>
    public decimal ParticipationTarget { get; init; } = 0.25m;
    public decimal RetentionTarget { get; init; } = 0.50m;
    /// <summary>The share of year groups with an ambassador that earns full marks on coverage.</summary>
    public decimal AmbassadorCoverageTarget { get; init; } = 0.70m;
    /// <summary>Share of the ambassador dimension that comes from coverage; the rest is how many of them are active.</summary>
    public decimal AmbassadorCoverageShare { get; init; } = 0.60m;
    /// <summary>A year group needs at least this many active members to count as needing an ambassador.</summary>
    public int MinimumCohortMembers { get; init; } = 3;
    /// <summary>An ambassador counts as active if they used the portal within this many days.</summary>
    public int AmbassadorActiveDays { get; init; } = 21;

    /// <summary>A pending member waiting this long or less is handled promptly (full marks); waiting <see cref="SlowPendingDays"/> or more earns none.</summary>
    public int PromptPendingDays { get; init; } = 2;
    public int SlowPendingDays { get; init; } = 14;

    /// <summary>Below this many active members a percentage says nothing, so no score is given.</summary>
    public int MinimumMembers { get; init; } = 5;
    /// <summary>Retention needs a previous-period group at least this large to mean anything.</summary>
    public int MinimumRetentionCohort { get; init; } = 5;

    public int HealthyFrom { get; init; } = 75;
    public int NeedsAttentionFrom { get; init; } = 50;
    public int AtRiskFrom { get; init; } = 25;
}

public static class HealthClassifications
{
    public const string Healthy = "Healthy";
    public const string NeedsAttention = "NeedsAttention";
    public const string AtRisk = "AtRisk";
    public const string Inactive = "Inactive";
    public const string InsufficientData = "InsufficientData";
}

public record HealthResult(int? Score, string Classification, IReadOnlyList<HealthFactor> Factors, string Summary);

/// <summary>
/// The Community Health Score: a weighted average of a few plain dimensions, each a percentage measured against a target.
/// A dimension with too little data behind it is left out and the rest are reweighted, rather than scored as zero.
/// Every factor carries its own explanation, so any score can be read back to the numbers that made it.
/// </summary>
public static class HealthScoreCalculator
{
    public static HealthResult Calculate(EngagementMetrics m, HealthScoreConfig? config = null)
    {
        config ??= new HealthScoreConfig();
        var dims = new List<(string Key, string Label, decimal Weight, int? Score, string Detail)>
        {
            Activation(m, config), Participation(m, config), Retention(m, config), Ambassadors(m, config), Responsiveness(m, config),
        };

        if (m.ActiveMembers < config.MinimumMembers)
        {
            var factors = dims.Select(d => new HealthFactor { Key = d.Key, Label = d.Label, ConfiguredWeight = d.Weight, EffectiveWeight = 0, Score = null, Detail = d.Detail }).ToList();
            return new HealthResult(null, HealthClassifications.InsufficientData, factors,
                $"Not enough members yet to score fairly. A reading appears once the community has {config.MinimumMembers} active members.");
        }

        var available = dims.Where(d => d.Score.HasValue && d.Weight > 0).ToList();
        var totalWeight = available.Sum(d => d.Weight);
        var result = dims.Select(d => new HealthFactor
        {
            Key = d.Key, Label = d.Label, ConfiguredWeight = d.Weight,
            EffectiveWeight = d.Score.HasValue && totalWeight > 0 ? Math.Round(d.Weight / totalWeight * 100, 1) : 0,
            Score = d.Score, Detail = d.Detail,
        }).ToList();

        var score = totalWeight == 0 ? 0 : (int)Math.Round(available.Sum(d => d.Score!.Value * d.Weight) / totalWeight, MidpointRounding.AwayFromZero);
        var classification = score >= config.HealthyFrom ? HealthClassifications.Healthy
            : score >= config.NeedsAttentionFrom ? HealthClassifications.NeedsAttention
            : score >= config.AtRiskFrom ? HealthClassifications.AtRisk
            : HealthClassifications.Inactive;
        return new HealthResult(score, classification, result, Describe(classification));
    }

    private static string Describe(string classification) => classification switch
    {
        HealthClassifications.Healthy => "Members are joining in and coming back.",
        HealthClassifications.NeedsAttention => "Some parts are working. The factors below show where to focus.",
        HealthClassifications.AtRisk => "Participation is low. A few well-chosen actions can turn it around.",
        _ => "Very little is happening. Start with the recommended actions.",
    };

    private static int Scaled(decimal rate, decimal target) => (int)Math.Round(Math.Min(1m, target <= 0 ? 0 : rate / target) * 100, MidpointRounding.AwayFromZero);
    private static string Pct(decimal rate) => $"{Math.Round(rate * 100):0}%";

    private static (string, string, decimal, int?, string) Activation(EngagementMetrics m, HealthScoreConfig c)
    {
        if (m.ActiveMembers == 0) return ("activation", "Member activation", c.ActivationWeight, null, "No active members yet.");
        var rate = (decimal)m.Activated / m.ActiveMembers;
        return ("activation", "Member activation", c.ActivationWeight, Scaled(rate, c.ActivationTarget),
            $"{m.Activated} of {m.ActiveMembers} members ({Pct(rate)}) have signed in and filled in part of their profile. Full marks at {Pct(c.ActivationTarget)}.");
    }

    private static (string, string, decimal, int?, string) Participation(EngagementMetrics m, HealthScoreConfig c)
    {
        if (m.ActiveMembers == 0) return ("participation", "Meaningful participation", c.ParticipationWeight, null, "No active members yet.");
        var rate = (decimal)m.Participants / m.ActiveMembers;
        return ("participation", "Meaningful participation", c.ParticipationWeight, Scaled(rate, c.ParticipationTarget),
            $"{m.Participants} of {m.ActiveMembers} members ({Pct(rate)}) did something meaningful in the last {m.PeriodDays} days: a discussion, an event sign-up, a gift or a class note. Full marks at {Pct(c.ParticipationTarget)}. Opening the portal does not count.");
    }

    private static (string, string, decimal, int?, string) Retention(EngagementMetrics m, HealthScoreConfig c)
    {
        if (m.PreviousParticipants < c.MinimumRetentionCohort)
            return ("retention", "Member retention", c.RetentionWeight, null,
                $"Needs at least {c.MinimumRetentionCohort} members who took part in the previous {m.PeriodDays} days to compare against. Left out of the score for now.");
        var rate = (decimal)m.RetainedParticipants / m.PreviousParticipants;
        return ("retention", "Member retention", c.RetentionWeight, Scaled(rate, c.RetentionTarget),
            $"{m.RetainedParticipants} of the {m.PreviousParticipants} members who took part in the previous {m.PeriodDays} days did so again ({Pct(rate)}). Full marks at {Pct(c.RetentionTarget)}.");
    }

    private static (string, string, decimal, int?, string) Ambassadors(EngagementMetrics m, HealthScoreConfig c)
    {
        const string key = "ambassadors", label = "Ambassador coverage and activity";
        if (!m.AmbassadorsAvailable || m.CohortsWithMembers == 0)
            return (key, label, c.AmbassadorWeight, null, "This institution has no year groups to cover, so this is left out of the score and the others carry its weight.");
        var coverage = (decimal)m.CohortsCovered / m.CohortsWithMembers;
        var activity = m.AmbassadorCount == 0 ? 0m : (decimal)m.ActiveAmbassadors / m.AmbassadorCount;
        var score = (int)Math.Round(Scaled(coverage, c.AmbassadorCoverageTarget) * c.AmbassadorCoverageShare + activity * 100 * (1 - c.AmbassadorCoverageShare), MidpointRounding.AwayFromZero);
        var detail = $"{m.CohortsCovered} of {m.CohortsWithMembers} year groups ({Pct(coverage)}) have an ambassador; full marks at {Pct(c.AmbassadorCoverageTarget)}. " +
            (m.AmbassadorCount == 0 ? "There are no ambassadors yet." : $"{m.ActiveAmbassadors} of {m.AmbassadorCount} ambassadors used the portal in the last 3 weeks.");
        return (key, label, c.AmbassadorWeight, score, detail);
    }

    private static (string, string, decimal, int?, string) Responsiveness(EngagementMetrics m, HealthScoreConfig c)
    {
        if (m.PendingMembers == 0 || m.OldestPendingDays is null)
            return ("responsiveness", "Administrator responsiveness", c.ResponsivenessWeight, 100, "Nobody is waiting for approval.");
        var days = m.OldestPendingDays.Value;
        var span = Math.Max(1, c.SlowPendingDays - c.PromptPendingDays);
        var score = days <= c.PromptPendingDays ? 100 : days >= c.SlowPendingDays ? 0 : (int)Math.Round(100m * (c.SlowPendingDays - days) / span);
        return ("responsiveness", "Administrator responsiveness", c.ResponsivenessWeight, score,
            $"{m.PendingMembers} {(m.PendingMembers == 1 ? "member is" : "members are")} waiting for approval; the longest has waited {days} {(days == 1 ? "day" : "days")}.");
    }
}

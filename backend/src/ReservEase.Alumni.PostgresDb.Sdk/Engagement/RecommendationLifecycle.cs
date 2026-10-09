using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

public record SyncPlan(
    IReadOnlyList<RecommendationCandidate> ToCreate,
    IReadOnlyList<EngagementRecommendation> ToReopen,
    IReadOnlyList<EngagementRecommendation> ToExpire);

/// <summary>
/// Decides, from what is already stored and what the rules want now, what to create, reopen and retire. Pure, so the
/// deduplication and cooldown rules can be tested without a database. Nothing here deletes: retired ones stay as history.
/// </summary>
public static class RecommendationLifecycle
{
    public static SyncPlan Plan(IReadOnlyCollection<EngagementRecommendation> existing, IReadOnlyCollection<RecommendationCandidate> candidates, DateTime now)
    {
        var wanted = candidates.Select(c => c.DedupeKey).ToHashSet();
        var toCreate = new List<RecommendationCandidate>();
        var toReopen = new List<EngagementRecommendation>();
        var toExpire = new List<EngagementRecommendation>();

        // Retire what no longer applies: an open one whose situation has cleared, or that passed its review date.
        foreach (var r in existing.Where(r => r.Status == RecommendationStatuses.Open))
            if (!wanted.Contains(r.DedupeKey) || (r.ExpiresAt is { } end && end <= now))
                toExpire.Add(r);

        foreach (var c in candidates)
        {
            var latest = existing.Where(r => r.DedupeKey == c.DedupeKey).OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            if (latest is null) { toCreate.Add(c); continue; }

            switch (latest.Status)
            {
                case RecommendationStatuses.Open:
                    break; // already showing; never raised twice
                case RecommendationStatuses.Snoozed:
                    if (latest.SnoozedUntil is { } until && until <= now) toReopen.Add(latest);
                    break;
                case RecommendationStatuses.Completed:
                case RecommendationStatuses.Dismissed:
                    // The same situation stays quiet for the rule's cooldown, then may return if it is still true.
                    if ((latest.ResolvedAt ?? latest.CreatedAt) + c.Cooldown <= now) toCreate.Add(c);
                    break;
                default: // Expired: the situation had cleared; it is back, so raise it again
                    toCreate.Add(c);
                    break;
            }
        }
        return new SyncPlan(toCreate, toReopen, toExpire);
    }
}

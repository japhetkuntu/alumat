using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

public sealed record MonthFigures(
    DateTime Month, int NewMembers, int Participants, int MeaningfulActions,
    decimal AmountCollected, int Contributors,
    int EventsHeld, int EventSignUps, int NewsPublished, int OpportunitiesShared,
    int SuggestionsRaised, int SuggestionsCompleted, int SuggestionsDismissed,
    int? HealthStart, int? HealthEnd, int HealthReadings);

public sealed record Computation(EngagementMetrics Metrics, IReadOnlyList<CohortRow> Cohorts, IReadOnlyList<AmbassadorRow> Ambassadors);

/// <summary>Everything one reading produces: the numbers, the score read from them, and how it compares with the last one.</summary>
public sealed record EngagementReading(EngagementMetrics Metrics, HealthResult Health, int? PreviousScore, IReadOnlyList<CohortRow> Cohorts, IReadOnlyList<AmbassadorRow> Ambassadors);

/// <summary>
/// Reads a community's engagement, scores it, records the day's reading and raises recommendations. Used by the Institution API
/// (when an administrator opens the page) and by the worker's daily job (so communities nobody visits still build a history),
/// which is why it lives here. It reads through the tenant-filtered repositories: the caller must have set the tenant on
/// <see cref="ICurrentTenantService"/> first, and a reading can only ever be about that one institution.
/// </summary>
public class EngagementEngine(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<ForumPost> postRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<ClassNote> classNoteRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<CampaignUpdate> campaignUpdateRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<CommunityHealthSnapshot> snapshotRepo,
    IAlumniPgRepository<EngagementRecommendation> recommendationRepo,
    IAlumniPgRepository<StaffEntity> staffRepo,
    IAlumniPgRepository<StaffActivityWeek> staffActivityRepo,
    IAlumniPgRepository<Referral> referralRepo,
    ICurrentTenantService currentTenant)
{
    private const int TrendWeeks = 8;

    private static bool IsUniqueViolation(DbUpdateException e) => e.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    private sealed class Act { public string MemberId { get; set; } = ""; public DateTime At { get; set; } }

    /// <summary>
    /// What happened in one calendar month. Activity (things done) and outcomes (what members did in response) are kept apart:
    /// ten announcements published says nothing about whether anyone benefited. Money is the amount members paid in. Fees are
    /// deliberately never part of this: the institution receives its full amount, so there is nothing to deduct and nothing to show.
    /// </summary>
    public async Task<MonthFigures> ComputeMonthAsync(DateTime monthStart)
    {
        monthStart = new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = monthStart.AddMonths(1);
        var activeIds = memberRepo.GetQueryable(m => m.Status == "Active").Select(m => m.Id);

        var acts = threadRepo.GetQueryable().Select(t => new Act { MemberId = t.AuthorId, At = t.CreatedAt })
            .Concat(postRepo.GetQueryable().Select(p => new Act { MemberId = p.AuthorId, At = p.CreatedAt }))
            .Concat(rsvpRepo.GetQueryable(r => r.Status == "Confirmed").Select(r => new Act { MemberId = r.MemberId, At = r.CreatedAt }))
            .Concat(contributionRepo.GetQueryable(c => c.Status == "Successful").Select(c => new Act { MemberId = c.MemberId, At = c.ConfirmedAt ?? c.CreatedAt }))
            .Concat(classNoteRepo.GetQueryable().Select(n => new Act { MemberId = n.AuthorId, At = n.CreatedAt }))
            .Where(a => a.At >= monthStart && a.At < end && activeIds.Contains(a.MemberId));
        var actions = await acts.CountAsync();
        var participants = await acts.Select(a => a.MemberId).Distinct().CountAsync();

        var paid = contributionRepo.GetQueryable(c => c.Status == "Successful" && (c.ConfirmedAt ?? c.CreatedAt) >= monthStart && (c.ConfirmedAt ?? c.CreatedAt) < end);
        var collected = await paid.SumAsync(c => (decimal?)c.Amount) ?? 0;

        var snapshots = await snapshotRepo.GetQueryable(s => s.PeriodDays == 30 && s.SnapshotDate >= monthStart && s.SnapshotDate < end && s.Score != null)
            .OrderBy(s => s.SnapshotDate).Select(s => new { s.Score, s.SnapshotDate }).ToListAsync();

        return new MonthFigures(
            Month: monthStart,
            NewMembers: await memberRepo.CountAsync(m => m.CreatedAt >= monthStart && m.CreatedAt < end),
            Participants: participants, MeaningfulActions: actions,
            AmountCollected: collected, Contributors: await paid.Select(c => c.MemberId).Distinct().CountAsync(),
            EventsHeld: await eventRepo.CountAsync(e => e.StartDate >= monthStart && e.StartDate < end && e.Status != "Cancelled"),
            EventSignUps: await rsvpRepo.CountAsync(r => r.Status == "Confirmed" && r.CreatedAt >= monthStart && r.CreatedAt < end),
            NewsPublished: await newsRepo.CountAsync(n => n.Status == "Published" && n.CreatedAt >= monthStart && n.CreatedAt < end),
            OpportunitiesShared: await jobRepo.CountAsync(j => j.Status != "Pending" && j.Status != "Draft" && j.CreatedAt >= monthStart && j.CreatedAt < end),
            SuggestionsRaised: await recommendationRepo.CountAsync(r => r.CreatedAt >= monthStart && r.CreatedAt < end),
            SuggestionsCompleted: await recommendationRepo.CountAsync(r => r.Status == RecommendationStatuses.Completed && r.ResolvedAt >= monthStart && r.ResolvedAt < end),
            SuggestionsDismissed: await recommendationRepo.CountAsync(r => r.Status == RecommendationStatuses.Dismissed && r.ResolvedAt >= monthStart && r.ResolvedAt < end),
            HealthStart: snapshots.Count == 0 ? null : snapshots[0].Score, HealthEnd: snapshots.Count == 0 ? null : snapshots[^1].Score, HealthReadings: snapshots.Count);
    }

    /// <summary>Reads, scores, records today's reading and syncs recommendations, in one idempotent pass.</summary>
    public async Task<EngagementReading> RefreshAsync(int days, ISet<string> disabledFeatures, bool usesYearGroups, DateTime now)
    {
        var computed = await ComputeAsync(now, days, usesYearGroups);
        var health = HealthScoreCalculator.Calculate(computed.Metrics);
        var previous = await SaveSnapshotAsync(now, days, computed.Metrics, health);
        await SyncRecommendationsAsync(computed.Metrics, disabledFeatures, now);
        return new EngagementReading(computed.Metrics, health, previous, computed.Cohorts, computed.Ambassadors);
    }

    public async Task<Computation> ComputeAsync(DateTime now, int days, bool usesYearGroups)
    {
        var start = now.AddDays(-days);
        var previousStart = now.AddDays(-2 * days);
        var weekStart = EngagementChecklist.WeekStart(now);
        var trendStart = weekStart.AddDays(-7 * (TrendWeeks - 1));

        var active = memberRepo.GetQueryable(m => m.Status == "Active");
        var activeIds = active.Select(m => m.Id);

        var activeCount = await active.CountAsync();
        var pending = memberRepo.GetQueryable(m => m.Status == "Pending" && m.IsEmailVerified);
        var pendingCount = await pending.CountAsync();
        var oldestPending = pendingCount == 0 ? (DateTime?)null : await pending.MinAsync(m => (DateTime?)m.CreatedAt);

        var newMembers = await memberRepo.CountAsync(m => m.CreatedAt >= start);
        var previousNew = await memberRepo.CountAsync(m => m.CreatedAt >= previousStart && m.CreatedAt < start);
        var joined7 = await active.CountAsync(m => m.CreatedAt >= now.AddDays(-7));

        var activated = await active.CountAsync(m => m.LastLoginAt != null
            && (m.Bio != null || m.JobTitle != null || m.Company != null || m.Location != null || m.ProfilePictureUrl != null));
        var signedIn7 = await active.CountAsync(m => m.LastLoginAt >= now.AddDays(-7));
        var signedIn30 = await active.CountAsync(m => m.LastLoginAt >= now.AddDays(-30));

        // Every meaningful action as (member, time), from the records the product already keeps.
        var acts = threadRepo.GetQueryable().Select(t => new Act { MemberId = t.AuthorId, At = t.CreatedAt })
            .Concat(postRepo.GetQueryable().Select(p => new Act { MemberId = p.AuthorId, At = p.CreatedAt }))
            .Concat(rsvpRepo.GetQueryable(r => r.Status == "Confirmed").Select(r => new Act { MemberId = r.MemberId, At = r.CreatedAt }))
            .Concat(contributionRepo.GetQueryable(c => c.Status == "Successful").Select(c => new Act { MemberId = c.MemberId, At = c.ConfirmedAt ?? c.CreatedAt }))
            .Concat(classNoteRepo.GetQueryable().Select(n => new Act { MemberId = n.AuthorId, At = n.CreatedAt }));

        var earliest = trendStart < previousStart ? trendStart : previousStart;
        var byMember = await acts.Where(a => a.At >= previousStart && activeIds.Contains(a.MemberId))
            .GroupBy(a => a.MemberId)
            .Select(g => new { Id = g.Key, Current = g.Any(a => a.At >= start), Previous = g.Any(a => a.At < start) })
            .ToListAsync();
        var participants = byMember.Where(x => x.Current).Select(x => x.Id).ToHashSet();
        var previousParticipants = byMember.Where(x => x.Previous).Select(x => x.Id).ToHashSet();

        var daily = await acts.Where(a => a.At >= earliest && activeIds.Contains(a.MemberId))
            .GroupBy(a => a.At.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync();
        var joinedByDay = await memberRepo.GetQueryable(m => m.CreatedAt >= trendStart)
            .GroupBy(m => m.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync();
        var weekly = Enumerable.Range(0, TrendWeeks).Select(i =>
        {
            var from = trendStart.AddDays(7 * i); var to = from.AddDays(7);
            return new WeeklyPoint(from,
                joinedByDay.Where(d => d.Day >= from && d.Day < to).Sum(d => d.Count),
                daily.Where(d => d.Day >= from && d.Day < to).Sum(d => d.Count));
        }).ToList();
        var actions7 = daily.Where(d => d.Day >= now.AddDays(-7).Date).Sum(d => d.Count);

        var dormantCandidates = await active.Where(m => m.CreatedAt < now.AddDays(-30) && (m.LastLoginAt == null || m.LastLoginAt < now.AddDays(-30)))
            .Select(m => m.Id).ToListAsync();
        var dormant = dormantCandidates.Count(id => !participants.Contains(id));

        var events = await eventRepo.GetQueryable(e => e.Status == "Upcoming" && e.StartDate >= now && e.StartDate <= now.AddDays(30))
            .OrderBy(e => e.StartDate).Take(5).Select(e => new { e.Id, e.Title, e.StartDate }).ToListAsync();
        var eventIds = events.Select(e => e.Id).ToList();
        var rsvps = eventIds.Count == 0 ? [] : await rsvpRepo.GetQueryable(r => eventIds.Contains(r.EventId) && r.Status == "Confirmed")
            .GroupBy(r => r.EventId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync();

        var campaigns = await campaignRepo.GetQueryable(c => c.Status == CampaignStatus.Active && c.Deadline > now && !c.IsMembershipCampaign)
            .OrderBy(c => c.Deadline).Take(10).Select(c => new { c.Id, c.Title, c.CreatedAt, c.Deadline }).ToListAsync();
        var campaignIds = campaigns.Select(c => c.Id).ToList();
        var lastUpdates = campaignIds.Count == 0 ? [] : await campaignUpdateRepo.GetQueryable(u => campaignIds.Contains(u.CampaignId))
            .GroupBy(u => u.CampaignId).Select(g => new { Id = g.Key, Last = g.Max(u => u.CreatedAt) }).ToListAsync();

        var paid = contributionRepo.GetQueryable(c => c.Status == "Successful");
        var volume = await paid.Where(c => (c.ConfirmedAt ?? c.CreatedAt) >= start).SumAsync(c => (decimal?)c.Amount) ?? 0;
        var previousVolume = await paid.Where(c => (c.ConfirmedAt ?? c.CreatedAt) >= previousStart && (c.ConfirmedAt ?? c.CreatedAt) < start).SumAsync(c => (decimal?)c.Amount) ?? 0;
        var contributors = await paid.Where(c => (c.ConfirmedAt ?? c.CreatedAt) >= start).Select(c => c.MemberId).Distinct().CountAsync();

        // ── Year groups and ambassadors ─────────────────────────────────────
        var config = new HealthScoreConfig();
        var cohorts = new List<CohortRow>();
        var ambassadors = new List<AmbassadorRow>();
        var uncovered = new List<UncoveredCohort>();
        var inactive = new List<InactiveAmbassador>();
        var cohortsWithMembers = 0; var cohortsCovered = 0; var activeAmbassadors = 0;
        if (usesYearGroups)
        {
            var perYear = await memberRepo.GetQueryable(m => (m.Status == "Active" || m.Status == "Pending") && m.GraduationYear > 0)
                .GroupBy(m => m.GraduationYear)
                .Select(g => new
                {
                    Year = g.Key,
                    Active = g.Count(m => m.Status == "Active"),
                    Pending = g.Count(m => m.Status == "Pending"),
                    New = g.Count(m => m.CreatedAt >= start),
                    Activated = g.Count(m => m.Status == "Active" && m.LastLoginAt != null && (m.Bio != null || m.JobTitle != null || m.Company != null || m.Location != null || m.ProfilePictureUrl != null)),
                    Incomplete = g.Count(m => m.Status == "Active" && m.Bio == null && m.JobTitle == null && m.Company == null && m.Location == null && m.ProfilePictureUrl == null),
                }).ToListAsync();

            var participantList = participants.ToList();
            var participantsByYear = participantList.Count == 0 ? [] : await memberRepo.GetQueryable(m => participantList.Contains(m.Id))
                .GroupBy(m => m.GraduationYear).Select(g => new { Year = g.Key, Count = g.Count() }).ToListAsync();

            // Invitations that became registrations, credited to the year group of the member who invited.
            var referrerIds = await referralRepo.GetQueryable(r => r.CreatedAt >= start && r.ReferredMemberId != null).Select(r => r.ReferrerId).ToListAsync();
            var distinctReferrers = referrerIds.Distinct().ToList();
            var referrerYears = distinctReferrers.Count == 0 ? [] : await memberRepo.GetQueryable(m => distinctReferrers.Contains(m.Id)).Select(m => new { m.Id, m.GraduationYear }).ToListAsync();
            var invitesByYear = referrerIds.Join(referrerYears, id => id, r => r.Id, (_, r) => r.GraduationYear).GroupBy(y => y).ToDictionary(g => g.Key, g => g.Count());

            var staff = await staffRepo.GetQueryable(x => x.Role == "ScopedAdmin" && !x.IsDisabled && x.YearGroups != null && x.YearGroups.Count > 0).ToListAsync();
            var staffIds = staff.Select(x => x.Id).ToList();
            var recentWeeks = staffIds.Count == 0 ? [] : await staffActivityRepo.GetQueryable(w => w.InstitutionId == currentTenant.InstitutionId && staffIds.Contains(w.StaffId) && w.WeekStart >= now.AddDays(-28))
                .GroupBy(w => w.StaffId).Select(g => new { Id = g.Key, Last = g.Max(w => w.WeekStart) }).ToListAsync();
            var tasks = staffIds.Count == 0 ? [] : await recommendationRepo.GetQueryable(r => r.AssignedToId != null && staffIds.Contains(r.AssignedToId))
                .GroupBy(r => new { r.AssignedToId, r.Status }).Select(g => new { Id = g.Key.AssignedToId, g.Key.Status, Count = g.Count() }).ToListAsync();

            var activeWindow = now.AddDays(-config.AmbassadorActiveDays);
            DateTime? LastActive(StaffEntity x)
            {
                var week = recentWeeks.FirstOrDefault(w => w.Id == x.Id)?.Last;
                var weekEnd = week?.AddDays(6);
                return x.LastLoginAt is { } login && (weekEnd is null || login > weekEnd) ? login : weekEnd ?? x.LastLoginAt;
            }

            var ambassadorStaff = staff.Select(x => (Staff: x, Last: LastActive(x))).ToList();
            foreach (var y in perYear.OrderBy(y => y.Year))
            {
                var covering = ambassadorStaff.Where(a => a.Staff.YearGroups!.Contains(y.Year)).ToList();
                cohorts.Add(new CohortRow(y.Year, y.Active, y.Pending, y.New, y.Activated, participantsByYear.FirstOrDefault(p => p.Year == y.Year)?.Count ?? 0,
                    invitesByYear.GetValueOrDefault(y.Year), y.Incomplete,
                    covering.Select(a => new AmbassadorRef(a.Staff.Id, $"{a.Staff.FirstName} {a.Staff.LastName}".Trim(), a.Last is { } l && l >= activeWindow)).ToList()));
            }
            var needing = cohorts.Where(c => c.ActiveMembers >= config.MinimumCohortMembers).ToList();
            cohortsWithMembers = needing.Count;
            cohortsCovered = needing.Count(c => c.Ambassadors.Count > 0);
            uncovered = needing.Where(c => c.Ambassadors.Count == 0).Select(c => new UncoveredCohort(c.Year, c.ActiveMembers)).ToList();

            foreach (var a in ambassadorStaff)
            {
                var isActive = a.Last is { } l && l >= activeWindow;
                var days0 = a.Last is { } seen ? Math.Max(0, (int)(now - seen).TotalDays) : int.MaxValue;
                var scope = cohorts.Where(c => a.Staff.YearGroups!.Contains(c.Year)).ToList();
                if (isActive) activeAmbassadors++;
                else inactive.Add(new InactiveAmbassador(a.Staff.Id, $"{a.Staff.FirstName} {a.Staff.LastName}".Trim(), days0 == int.MaxValue ? 0 : days0));
                ambassadors.Add(new AmbassadorRow(a.Staff.Id, $"{a.Staff.FirstName} {a.Staff.LastName}".Trim(), a.Staff.YearGroups!, a.Last, days0 == int.MaxValue ? -1 : days0, isActive,
                    tasks.Where(t => t.Id == a.Staff.Id && (t.Status == RecommendationStatuses.Open || t.Status == RecommendationStatuses.Snoozed)).Sum(t => t.Count),
                    tasks.Where(t => t.Id == a.Staff.Id && t.Status == RecommendationStatuses.Completed).Sum(t => t.Count),
                    scope.Sum(c => c.ActiveMembers), scope.Sum(c => c.NewMembers)));
            }
        }

        var metrics = new EngagementMetrics
        {
            Now = now, PeriodDays = days,
            ActiveMembers = activeCount, PendingMembers = pendingCount,
            OldestPendingDays = oldestPending is null ? null : Math.Max(0, (int)(now - oldestPending.Value).TotalDays),
            NewMembers = newMembers, PreviousNewMembers = previousNew, JoinedLast7Days = joined7,
            Activated = activated, SignedInLast7Days = signedIn7, SignedInLast30Days = signedIn30,
            Participants = participants.Count, PreviousParticipants = previousParticipants.Count,
            RetainedParticipants = previousParticipants.Count(participants.Contains),
            MeaningfulActionsLast7Days = actions7, Dormant = dormant,
            NewsPublishedThisWeek = await newsRepo.CountAsync(n => n.Status == "Published" && n.CreatedAt >= weekStart),
            JobsPostedThisWeek = await jobRepo.CountAsync(j => j.CreatedAt >= weekStart && j.Status != "Pending"),
            PendingOpportunities = await jobRepo.CountAsync(j => j.Status == "Pending"),
            UpcomingEvents = events.Select(e => new UpcomingEventInfo(e.Id, e.Title, e.StartDate, rsvps.FirstOrDefault(r => r.Id == e.Id)?.Count ?? 0)).ToList(),
            ActiveCampaigns = campaigns.Select(c => new ActiveCampaignInfo(c.Id, c.Title, c.CreatedAt, lastUpdates.FirstOrDefault(u => u.Id == c.Id)?.Last, c.Deadline)).ToList(),
            ContributionVolume = volume, PreviousContributionVolume = previousVolume, Contributors = contributors,
            Weekly = weekly,
            AmbassadorsAvailable = usesYearGroups && cohortsWithMembers > 0, CohortsWithMembers = cohortsWithMembers, CohortsCovered = cohortsCovered,
            AmbassadorCount = ambassadors.Count, ActiveAmbassadors = activeAmbassadors, UncoveredCohorts = uncovered, InactiveAmbassadors = inactive,
        };
        return new Computation(metrics, cohorts, ambassadors);
    }

    /// <summary>Records today's reading (replacing an earlier one from the same day) and returns the latest earlier score, if any.</summary>
    public async Task<int?> SaveSnapshotAsync(DateTime now, int days, EngagementMetrics m, HealthResult health)
    {
        var today = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var existing = await snapshotRepo.GetOneAsync(s => s.PeriodDays == days && s.SnapshotDate == today);
        if (existing is null)
        {
            var created = new CommunityHealthSnapshot
            {
                InstitutionId = currentTenant.InstitutionId!, SnapshotDate = today, PeriodDays = days,
                Score = health.Score, Classification = health.Classification, ActiveMembers = m.ActiveMembers, Factors = health.Factors.ToList(),
            };
            try { await snapshotRepo.AddAsync(created); }
            catch (DbUpdateException e) when (IsUniqueViolation(e))
            {
                // Another request (or the daily worker) recorded today's reading a moment ago: fall through and update theirs instead.
                snapshotRepo.Detach(created);
                existing = await snapshotRepo.GetOneAsync(s => s.PeriodDays == days && s.SnapshotDate == today);
            }
        }
        if (existing is not null)
        {
            existing.Score = health.Score; existing.Classification = health.Classification; existing.ActiveMembers = m.ActiveMembers;
            existing.Factors = health.Factors.ToList(); existing.UpdatedAt = now;
            await snapshotRepo.UpdateAsync(existing);
        }
        return (await snapshotRepo.GetQueryable(s => s.PeriodDays == days && s.SnapshotDate < today && s.Score != null)
            .OrderByDescending(s => s.SnapshotDate).Select(s => s.Score).FirstOrDefaultAsync());
    }

    public async Task SyncRecommendationsAsync(EngagementMetrics m, ISet<string> disabled, DateTime now)
    {
        var candidates = RecommendationRules.Evaluate(m, disabled);
        var existing = await recommendationRepo.GetQueryable(r => r.CreatedAt >= now.AddDays(-90)).ToListAsync();
        var plan = RecommendationLifecycle.Plan(existing, candidates, now);

        foreach (var r in plan.ToExpire) { r.Status = RecommendationStatuses.Expired; r.UpdatedAt = now; }
        foreach (var r in plan.ToReopen) { r.Status = RecommendationStatuses.Open; r.SnoozedUntil = null; r.UpdatedAt = now; }
        if (plan.ToExpire.Count + plan.ToReopen.Count > 0) await recommendationRepo.UpdateRangeAsync(plan.ToExpire.Concat(plan.ToReopen).ToList());
        // One at a time: the database refuses a second live copy of the same situation, and when another request got there first
        // that one is simply skipped, instead of failing the whole batch.
        foreach (var c in plan.ToCreate)
        {
            var row = new EngagementRecommendation
            {
                InstitutionId = currentTenant.InstitutionId!, RuleId = c.RuleId, DedupeKey = c.DedupeKey, Title = c.Title,
                Explanation = c.Explanation, Priority = c.Priority, ActionLabel = c.ActionLabel, ActionUrl = c.ActionUrl, ExpiresAt = c.ExpiresAt,
            };
            try { await recommendationRepo.AddAsync(row); }
            catch (DbUpdateException e) when (IsUniqueViolation(e)) { recommendationRepo.Detach(row); }
        }
    }
}

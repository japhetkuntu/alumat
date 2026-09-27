using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>One activation criterion's outcome, with a short human-readable measurement ("142 members · 38% signed in").</summary>
public sealed record ActivationCriterion(string Key, string Label, bool Met, string Detail);

public sealed record InstitutionActivation(
    string InstitutionId,
    string InstitutionName,
    string Slug,
    DateTime OnboardedAt,
    DateTime? ActivatedAt,
    int DaysLive,
    List<ActivationCriterion> Criteria,
    /// <summary>Key of the first unmet criterion in playbook order, or null when all are met.</summary>
    string? NextStepKey,
    /// <summary>What the institution should do next, phrased for its own admins. Null when all are met.</summary>
    string? NextStep,
    /// <summary>Live for longer than <see cref="InstitutionActivationService.StalledAfterDays"/> without meeting every criterion.</summary>
    bool IsStalled,
    /// <summary>Past the <see cref="InstitutionActivationService.ActivationWindowDays"/>-day activation window without meeting every criterion.</summary>
    bool IsOverdue,
    /// <summary>The member threshold applied — the institution's own override or the platform default.</summary>
    int MinMembers,
    DateTime? TrialEndsAt,
    bool SetupNudgesEnabled)
{
    public bool AllMet => Criteria.All(c => c.Met);
    public int MetCount => Criteria.Count(c => c.Met);
}

/// <summary>
/// The single definition of "this institution is actively using the platform",
/// shared by Platform.Api's Activation page, Institution.Api's setup checklist
/// and the Operations Worker's nudges, so they can never disagree. Five criteria,
/// checked in onboarding-playbook order: branding → payouts → members → first
/// campaign and online payment → staff using the portal week after week.
/// Evaluates any number of institutions in a fixed handful of grouped queries,
/// not one round-trip per institution.
/// </summary>
public class InstitutionActivationService(
    IAlumniPgRepository<InstitutionStaff> staffRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    IAlumniPgRepository<StaffActivityWeek> staffActivityRepo,
    IAlumniPgRepository<InstitutionActivitySnapshot> snapshotRepo)
{
    public const int MinMembers = 100;
    public const double MinSignedInShare = 0.30;
    public const int MinWeeklyActiveStaff = 2;
    public const int RequiredConsecutiveWeeks = 3;
    public const int StalledAfterDays = 14;
    public const int ActivationWindowDays = 30;

    public const string Branding = "branding";
    public const string Payouts = "payouts";
    public const string Members = "members";
    public const string Payments = "payments";
    public const string StaffActivity = "staff";

    /// <summary>Monday 00:00 UTC of the week containing <paramref name="utc"/>.</summary>
    public static DateTime WeekStart(DateTime utc)
    {
        var daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(utc.Date.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }

    public async Task<InstitutionActivation?> EvaluateOneAsync(Institution institution, DateTime now) =>
        (await EvaluateAsync([institution], now)).SingleOrDefault();

    public async Task<List<InstitutionActivation>> EvaluateAsync(IReadOnlyCollection<Institution> institutions, DateTime now)
    {
        if (institutions.Count == 0) return [];
        var ids = institutions.Select(i => i.Id).ToList();
        var weekStart = WeekStart(now);
        // Current week plus enough history to see a full streak that ended last week.
        var historyStart = weekStart.AddDays(-7 * RequiredConsecutiveWeeks);

        var memberStats = await memberRepo.GetQueryable(m => ids.Contains(m.InstitutionId) && (m.Status == "Active" || m.Status == "Pending"), ignoreQueryFilters: true)
            .GroupBy(m => m.InstitutionId)
            .Select(g => new
            {
                InstitutionId = g.Key,
                Total = g.Count(m => m.Status == "Active"),
                SignedIn = g.Count(m => m.Status == "Active" && m.LastLoginAt != null),
                Pending = g.Count(m => m.Status == "Pending"),
            })
            .ToDictionaryAsync(x => x.InstitutionId);

        var activeCampaignCounts = await campaignRepo.GetQueryable(c => ids.Contains(c.InstitutionId) && c.Status == CampaignStatus.Active, ignoreQueryFilters: true)
            .GroupBy(c => c.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InstitutionId, x => x.Count);

        // "Online" = went through Paystack — dues/donations (one-off or recurring), store
        // orders and paid service requests. Manual/cash entries prove bookkeeping, not
        // that members can pay through the portal themselves.
        var contributionCounts = await contributionRepo.GetQueryable(
                c => ids.Contains(c.InstitutionId) && c.Status == "Successful" && c.PaymentMethod.StartsWith("Paystack"),
                ignoreQueryFilters: true)
            .GroupBy(c => c.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InstitutionId, x => x.Count);
        var storeCounts = await storeOrderRepo.GetQueryable(
                o => ids.Contains(o.InstitutionId) && o.Status == "Successful" && o.PaymentMethod.StartsWith("Paystack"),
                ignoreQueryFilters: true)
            .GroupBy(o => o.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InstitutionId, x => x.Count);
        var serviceCounts = await serviceRequestRepo.GetQueryable(
                r => ids.Contains(r.InstitutionId) && r.PaymentStatus == "Successful" && r.PaymentMethod.StartsWith("Paystack"),
                ignoreQueryFilters: true)
            .GroupBy(r => r.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InstitutionId, x => x.Count);

        // Weekly active staff: StaffActivityWeek is the primary record (written at sign-in
        // and session refresh). Older snapshot rows and this week's LastLoginAt are folded
        // in with Max, so history from before StaffActivityWeek existed still counts.
        var activityWeeks = await staffActivityRepo.GetQueryable(a => ids.Contains(a.InstitutionId) && a.WeekStart >= historyStart)
            .GroupBy(a => new { a.InstitutionId, a.WeekStart })
            .Select(g => new { g.Key.InstitutionId, g.Key.WeekStart, Count = g.Count() }) // one row per staff per week (unique index)
            .ToListAsync();
        var liveActiveStaff = await staffRepo.GetQueryable(
                s => ids.Contains(s.InstitutionId) && !s.IsDisabled && s.LastLoginAt >= weekStart, ignoreQueryFilters: true)
            .GroupBy(s => s.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InstitutionId, x => x.Count);
        var snapshots = await snapshotRepo.GetQueryable(a => ids.Contains(a.InstitutionId) && a.WeekStart >= historyStart)
            .Select(a => new { a.InstitutionId, a.WeekStart, a.ActiveStaffCount })
            .ToListAsync();

        var weeklyByInstitution = new Dictionary<string, Dictionary<DateTime, int>>();
        void Fold(string institutionId, DateTime week, int count)
        {
            if (!weeklyByInstitution.TryGetValue(institutionId, out var weeks))
                weeklyByInstitution[institutionId] = weeks = new Dictionary<DateTime, int>();
            weeks[week] = Math.Max(weeks.GetValueOrDefault(week), count);
        }
        foreach (var a in activityWeeks) Fold(a.InstitutionId, DateTime.SpecifyKind(a.WeekStart, DateTimeKind.Utc), a.Count);
        foreach (var s in snapshots) Fold(s.InstitutionId, DateTime.SpecifyKind(s.WeekStart, DateTimeKind.Utc), s.ActiveStaffCount);
        foreach (var (institutionId, count) in liveActiveStaff) Fold(institutionId, weekStart, count);

        var results = new List<InstitutionActivation>(institutions.Count);
        foreach (var inst in institutions)
        {
            var criteria = new List<ActivationCriterion>(5);

            var brandingMissing = new List<string>();
            if (string.IsNullOrWhiteSpace(inst.LogoUrl)) brandingMissing.Add("logo");
            if (inst.HeroImageUrls.Count == 0) brandingMissing.Add("hero photo");
            if (inst.LandingPageStories.Count == 0) brandingMissing.Add("landing stories");
            criteria.Add(new ActivationCriterion(Branding, "Branded portal", brandingMissing.Count == 0,
                brandingMissing.Count == 0 ? "Logo, hero photo and stories set" : $"Missing {string.Join(", ", brandingMissing)}"));

            criteria.Add(new ActivationCriterion(Payouts, "Payouts live", inst.PayoutStatus == "Approved",
                inst.PayoutStatus switch
                {
                    "Approved" => "Settlement account approved",
                    "Pending" => "Bank details awaiting platform review",
                    "Rejected" => "Bank details rejected, resubmit",
                    _ => "No bank details submitted",
                }));

            var minMembers = inst.ActivationMinMembers is > 0 ? inst.ActivationMinMembers.Value : MinMembers;
            var ms = memberStats.GetValueOrDefault(inst.Id);
            var total = ms?.Total ?? 0;
            var signedIn = ms?.SignedIn ?? 0;
            var pending = ms?.Pending ?? 0;
            var share = total == 0 ? 0 : (double)signedIn / total;
            criteria.Add(new ActivationCriterion(Members, "Members on board",
                total >= minMembers && share >= MinSignedInShare,
                $"{total:N0}/{minMembers:N0} members · {Math.Round(share * 100)}% signed in"
                + (pending > 0 ? $" · {pending:N0} awaiting approval" : "")));

            var campaigns = activeCampaignCounts.GetValueOrDefault(inst.Id);
            var payments = contributionCounts.GetValueOrDefault(inst.Id) + storeCounts.GetValueOrDefault(inst.Id) + serviceCounts.GetValueOrDefault(inst.Id);
            criteria.Add(new ActivationCriterion(Payments, "Campaign and online payment",
                campaigns > 0 && payments > 0,
                $"{campaigns} active campaign{(campaigns == 1 ? "" : "s")} · {payments:N0} online payment{(payments == 1 ? "" : "s")}"));

            var weekly = weeklyByInstitution.GetValueOrDefault(inst.Id) ?? new Dictionary<DateTime, int>();
            var streak = StaffStreak(weekly, weekStart);
            criteria.Add(new ActivationCriterion(StaffActivity, "Staff active weekly",
                streak >= RequiredConsecutiveWeeks,
                $"{weekly.GetValueOrDefault(weekStart)} staff this week · {streak}/{RequiredConsecutiveWeeks} week streak"));

            var next = criteria.FirstOrDefault(c => !c.Met);
            var daysLive = Math.Max(0, (int)(now - inst.OnboardedAt).TotalDays);
            results.Add(new InstitutionActivation(
                inst.Id, inst.Name, inst.Slug, inst.OnboardedAt, inst.ActivatedAt, daysLive, criteria,
                next?.Key, next is null ? null : NextStepFor(next.Key, inst, total, share, pending, minMembers),
                IsStalled: next is not null && daysLive > StalledAfterDays,
                IsOverdue: next is not null && inst.ActivatedAt is null && daysLive > ActivationWindowDays,
                MinMembers: minMembers,
                TrialEndsAt: inst.TrialEndsAt,
                SetupNudgesEnabled: inst.SetupNudgesEnabled));
        }
        return results;
    }

    /// <summary>
    /// Consecutive weeks with at least <see cref="MinWeeklyActiveStaff"/> active staff,
    /// counting back from this week — or from last week if this week hasn't reached
    /// the bar yet, so a Monday morning doesn't read as a broken streak.
    /// </summary>
    private static int StaffStreak(Dictionary<DateTime, int> weekly, DateTime weekStart)
    {
        var cursor = weekly.GetValueOrDefault(weekStart) >= MinWeeklyActiveStaff ? weekStart : weekStart.AddDays(-7);
        var streak = 0;
        while (weekly.GetValueOrDefault(cursor) >= MinWeeklyActiveStaff)
        {
            streak++;
            cursor = cursor.AddDays(-7);
        }
        return streak;
    }

    private static string NextStepFor(string key, Institution inst, int members, double share, int pending, int minMembers) => key switch
    {
        Branding => "Finish your portal's branding in Settings: add your logo, then a hero photo and at least one \"why alumni join\" story under Landing content, so members recognise the portal as yours when you share the link.",
        Payouts => inst.PayoutStatus == "Pending"
            ? "Your settlement bank details are with our team for review. We'll confirm shortly, and online payments go live as soon as they're approved."
            : "Submit your settlement bank details in Settings (Payout setup) so online dues and donations can reach your account.",
        // Pending approvals first: they're members who already signed up and are waiting on the institution.
        Members when pending > 0 && members < minMembers =>
            $"{pending:N0} member{(pending == 1 ? " is" : "s are")} waiting for approval under Members. Approve them so they can get into the portal.",
        Members => members < minMembers
            ? $"You have {members:N0} of {minMembers:N0} members on the portal. Upload the rest of your roster from Members → Bulk upload so everyone can be reached in one place."
            : $"{Math.Round(share * 100)}% of your members have signed in. Share the portal link in your WhatsApp groups and send a broadcast inviting them to log in.",
        Payments => "Launch your first campaign (this year's dues or a fundraising drive) and share it with members, so the first online payment comes through.",
        StaffActivity => "Invite at least one more executive under Staff, and have your team check the portal each week to approve members and follow up on payments.",
        _ => string.Empty,
    };
}

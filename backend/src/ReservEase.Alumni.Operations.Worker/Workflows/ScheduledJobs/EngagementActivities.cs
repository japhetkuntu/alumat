using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Activities;
using Temporalio.Exceptions;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Automated community engagement: keeps each institution's health reading and recommendations current without anyone opening
/// the page, reminds administrators who have been away when there is real work waiting, and sends a quiet-member note only
/// when there is something real to show. Every message goes through <see cref="AutomationPolicy"/> (windows, cooldowns, caps)
/// and is written to <see cref="EngagementMessage"/> first, so a retry or a second run cannot send it twice.
/// Each activity sets the tenant on its own scope, then reads through the tenant-filtered repositories.
/// </summary>
public class EngagementActivities(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<StaffEntity> staffRepo,
    IAlumniPgRepository<StaffActivityWeek> staffActivityRepo,
    IAlumniPgRepository<EngagementRecommendation> recommendationRepo,
    IAlumniPgRepository<EngagementMessage> messageRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<ForumPost> postRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<ClassNote> classNoteRepo,
    EngagementEngine engine,
    ICurrentTenantService currentTenant,
    ITemporalClientProvider temporalProvider,
    IConfiguration configuration,
    ILogger<EngagementActivities> logger)
{
    /// <summary>
    /// The limits, with the defaults from <see cref="AutomationLimits"/> unless the "EngagementAutomation" configuration section says
    /// otherwise (for example SendWindowStartHour/SendWindowEndHour for an operator whose members are not on GMT).
    /// </summary>
    private AutomationLimits Limits { get; } = ReadLimits(configuration);

    private static AutomationLimits ReadLimits(IConfiguration configuration)
    {
        var d = new AutomationLimits();
        var section = configuration.GetSection("EngagementAutomation");
        int Int(string key, int fallback) => int.TryParse(section[key], out var v) ? v : fallback;
        return d with
        {
            SendWindowStartHour = Math.Clamp(Int(nameof(AutomationLimits.SendWindowStartHour), d.SendWindowStartHour), 0, 23),
            SendWindowEndHour = Math.Clamp(Int(nameof(AutomationLimits.SendWindowEndHour), d.SendWindowEndHour), 1, 24),
            MaxMemberMessagesPerRun = Math.Clamp(Int(nameof(AutomationLimits.MaxMemberMessagesPerRun), d.MaxMemberMessagesPerRun), 0, 1000),
        };
    }

    /// <summary>The time source. Tests replace it so the daytime-window rule does not depend on when they run.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    private DateTime Now => Clock.GetUtcNow().UtcDateTime;

    private async Task<Institution?> EnterAsync(string institutionId)
    {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId && i.Status == "Active", ignoreQueryFilters: true);
        if (institution is not null) currentTenant.SetInstitutionId(institution.Id, institution.Slug);
        return institution;
    }

    [Activity("Engagement.ResolveInstitutionIds")]
    public virtual Task<List<string>> ResolveInstitutionIdsAsync() =>
        Wrap(async () => (await institutionRepo.GetQueryable(i => i.Status == "Active", ignoreQueryFilters: true).Select(i => i.Id).ToListAsync()), "list institutions", "all");

    /// <summary>Records today's reading and refreshes recommendations. Idempotent: running it twice in a day changes nothing the second time.</summary>
    [Activity("Engagement.RefreshInstitution")]
    public virtual Task<int> RefreshInstitutionAsync(string institutionId) =>
        Wrap(async () =>
        {
            var institution = await EnterAsync(institutionId);
            if (institution is null) return 0;
            var reading = await engine.RefreshAsync(30, institution.DisabledFeatures.ToHashSet(), institution.OrganizationType != "Community", Now);
            return reading.Metrics.ActiveMembers;
        }, "refresh engagement", institutionId);

    // ── Administrators ──────────────────────────────────────────────────────

    [Activity("Engagement.SendAdminMessages")]
    public virtual Task<int> SendAdminMessagesAsync(string institutionId) =>
        Wrap(async () =>
        {
            var now = Now;
            if (!AutomationPolicy.InSendWindow(now, Limits)) return 0;
            var institution = await EnterAsync(institutionId);
            if (institution is null || !institution.EmailNotificationsEnabled) return 0;

            var admins = await staffRepo.GetQueryable(s => s.Role == "SuperAdmin" && !s.IsDisabled).ToListAsync();
            if (admins.Count == 0) return 0;
            var ids = admins.Select(a => a.Id).ToList();
            var weeks = await staffActivityRepo.GetQueryable(w => w.InstitutionId == institutionId && ids.Contains(w.StaffId))
                .GroupBy(w => w.StaffId).Select(g => new { Id = g.Key, Last = g.Max(w => w.WeekStart) }).ToListAsync();
            var states = admins.Select(a =>
            {
                var weekEnd = weeks.FirstOrDefault(w => w.Id == a.Id)?.Last.AddDays(6);
                var last = a.LastLoginAt is { } login && (weekEnd is null || login > weekEnd) ? login : weekEnd ?? a.LastLoginAt;
                return new StaffState(a.Id, $"{a.FirstName} {a.LastName}".Trim(), a.Email, last);
            }).ToList();

            var open = await recommendationRepo.CountAsync(r => r.Status == RecommendationStatuses.Open);
            var pending = await memberRepo.CountAsync(m => m.Status == "Pending" && m.IsEmailVerified);
            var sent = await SentLogAsync(now.AddDays(-60), [EngagementMessageKinds.AdminReminder, EngagementMessageKinds.AdminEscalation]);
            var plans = AutomationPolicy.PlanAdminMessages(states, open, pending, now, sent, Limits);
            if (plans.Count == 0) return 0;

            var brand = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
            var adminDomain = configuration["AdminPortalBaseDomain"];
            var link = string.IsNullOrWhiteSpace(adminDomain) ? "" : $"https://{institution.Slug}.{adminDomain}/engagement";
            var nameById = states.ToDictionary(s => s.StaffId, s => s.Name);
            var count = 0;
            foreach (var p in plans)
            {
                // The log row goes in first: if sending then fails, the cooldown still holds and a retry cannot double-send.
                await messageRepo.AddAsync(new EngagementMessage { InstitutionId = institutionId, RecipientType = "Staff", RecipientId = p.RecipientId, Kind = p.Kind, Subject = p.Subject, CreatedBy = "system" });
                var (title, body) = p.Kind == EngagementMessageKinds.AdminReminder
                    ? EngagementMessageText.AdminReminder(brand, p.DaysAway, open, pending)
                    : EngagementMessageText.AdminEscalation(brand, nameById.GetValueOrDefault(p.Subject ?? "", ""), p.DaysAway);
                await SendAsync(institution, p.RecipientName, p.RecipientEmail, title, body, "Open Engagement", link, "Community update", $"engagement {p.Kind} to {p.RecipientEmail}");
                count++;
            }
            logger.LogInformation("Engagement admin messages for {InstitutionId}: {Count}", institutionId, count);
            return count;
        }, "send admin engagement messages", institutionId);

    // ── Members ─────────────────────────────────────────────────────────────

    private sealed record Item(string Text, List<int>? YearGroups);

    [Activity("Engagement.SendMemberReengagement")]
    public virtual Task<int> SendMemberReengagementAsync(string institutionId) =>
        Wrap(async () =>
        {
            var now = Now;
            if (!AutomationPolicy.InSendWindow(now, Limits)) return 0;
            var institution = await EnterAsync(institutionId);
            if (institution is null || !institution.EmailNotificationsEnabled) return 0;

            // What there really is to show. Nothing here is invented; with none of it, nobody is written to.
            var events = await eventRepo.GetQueryable(e => e.Status == "Upcoming" && e.CommunityId == null && e.StartDate >= now && e.StartDate <= now.AddDays(14))
                .OrderBy(e => e.StartDate).Take(5).Select(e => new { e.Title, e.StartDate, e.YearGroups }).ToListAsync();
            var jobs = await jobRepo.GetQueryable(j => j.Status == "Active" && j.CommunityId == null && j.CreatedAt >= now.AddDays(-30) && (j.Deadline == null || j.Deadline >= now.Date))
                .OrderByDescending(j => j.CreatedAt).Take(5).Select(j => new { j.Title, j.Company, j.YearGroups }).ToListAsync();
            var news = await newsRepo.GetQueryable(n => n.Status == "Published" && n.CommunityId == null && n.CreatedAt >= now.AddDays(-30))
                .OrderByDescending(n => n.CreatedAt).Take(3).Select(n => new { n.Title, n.YearGroups }).ToListAsync();
            var content = events.Select(e => new Item($"Coming up: {e.Title}, {e.StartDate:d MMM}", e.YearGroups))
                .Concat(jobs.Select(j => new Item($"New opportunity: {j.Title} at {j.Company}", j.YearGroups)))
                .Concat(news.Select(n => new Item($"News: {n.Title}", n.YearGroups))).ToList();
            if (content.Count == 0) return 0;

            var quietSince = now.AddDays(-Limits.MemberQuietDays);
            var candidates = await memberRepo.GetQueryable(m => m.Status == "Active" && m.CreatedAt < quietSince && (m.LastLoginAt == null || m.LastLoginAt < quietSince))
                .OrderBy(m => m.LastLoginAt).Take(500).ToListAsync();
            if (candidates.Count == 0) return 0;
            var ids = candidates.Select(c => c.Id).ToList();

            var active = new HashSet<string>();
            active.UnionWith(await threadRepo.GetQueryable(t => t.CreatedAt >= quietSince && ids.Contains(t.AuthorId)).Select(t => t.AuthorId).Distinct().ToListAsync());
            active.UnionWith(await postRepo.GetQueryable(p => p.CreatedAt >= quietSince && ids.Contains(p.AuthorId)).Select(p => p.AuthorId).Distinct().ToListAsync());
            active.UnionWith(await rsvpRepo.GetQueryable(r => r.CreatedAt >= quietSince && r.Status == "Confirmed" && ids.Contains(r.MemberId)).Select(r => r.MemberId).Distinct().ToListAsync());
            active.UnionWith(await contributionRepo.GetQueryable(c => c.Status == "Successful" && c.CreatedAt >= quietSince && ids.Contains(c.MemberId)).Select(c => c.MemberId).Distinct().ToListAsync());
            active.UnionWith(await classNoteRepo.GetQueryable(n => n.CreatedAt >= quietSince && ids.Contains(n.AuthorId)).Select(n => n.AuthorId).Distinct().ToListAsync());

            var optedOut = (await prefRepo.GetQueryable(p => ids.Contains(p.MemberId) && !p.EngagementMessages).Select(p => p.MemberId).ToListAsync()).ToHashSet();
            var history = await messageRepo.GetQueryable(m => m.RecipientType == "Member" && m.CreatedAt >= now.AddDays(-60) && ids.Contains(m.RecipientId))
                .Select(m => new { m.RecipientId, m.Kind, m.CreatedAt }).ToListAsync();

            var portal = MemberPortalLinks.UrlOrEmpty(institution.Slug, institution.CustomDomain, configuration["MemberPortalBaseDomain"]);
            var sentCount = 0;
            foreach (var m in candidates)
            {
                if (sentCount >= Limits.MaxMemberMessagesPerRun) break;
                var mine = content.Where(i => i.YearGroups is null or { Count: 0 } || i.YearGroups.Contains(m.GraduationYear)).Take(3).ToList();
                var lastRe = history.Where(h => h.RecipientId == m.Id && h.Kind == EngagementMessageKinds.MemberReengagement).Select(h => (DateTime?)h.CreatedAt).Max();
                var lastAny = history.Where(h => h.RecipientId == m.Id).Select(h => (DateTime?)h.CreatedAt).Max();
                if (!AutomationPolicy.ShouldReengage(optedOut.Contains(m.Id), institution.EmailNotificationsEnabled, !string.IsNullOrWhiteSpace(m.Email),
                        m.CreatedAt, m.LastLoginAt, active.Contains(m.Id), lastRe, lastAny, mine.Count, now, Limits)) continue;

                await messageRepo.AddAsync(new EngagementMessage { InstitutionId = institutionId, RecipientType = "Member", RecipientId = m.Id, Kind = EngagementMessageKinds.MemberReengagement, CreatedBy = "system" });
                var brand = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
                var (title, body) = EngagementMessageText.QuietMember(brand, mine.Select(i => i.Text).ToList());
                await SendAsync(institution, m.FirstName, m.Email, title, body, "See what's new", portal, "Community update", $"engagement reengagement to {m.Email}");
                sentCount++;
            }
            logger.LogInformation("Engagement re-engagement for {InstitutionId}: {Sent} sent of {Candidates} quiet candidates", institutionId, sentCount, candidates.Count);
            return sentCount;
        }, "send member re-engagement", institutionId);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Dictionary<(string, string, string?), DateTime>> SentLogAsync(DateTime since, string[] kinds)
    {
        var rows = await messageRepo.GetQueryable(m => m.CreatedAt >= since && kinds.Contains(m.Kind)).Select(m => new { m.RecipientId, m.Kind, m.Subject, m.CreatedAt }).ToListAsync();
        return rows.GroupBy(r => (r.RecipientId, r.Kind, r.Subject)).ToDictionary(g => g.Key, g => g.Max(r => r.CreatedAt));
    }

    private Task SendAsync(Institution institution, string firstName, string email, string title, string body, string actionLabel, string actionUrl, string badge, string context) =>
        temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(new SendEmailRequest
        {
            To = [new EmailContact { Email = email, Name = firstName }],
            TemplateId = "notification",
            TemplateVariables = new
            {
                first_name = firstName, title, body, badge_label = badge, action_url = actionUrl, action_label = actionLabel,
                brand_name = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName,
                brand_color = institution.PrimaryColorHex, brand_secondary_color = institution.SecondaryColorHex, brand_logo = institution.LogoUrl,
            },
        }, context), logger);

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try { return await action(); }
        catch (Exception ex) { throw new ApplicationFailureException($"Failed to {what} ({context})", ex); }
    }
}

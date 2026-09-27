using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Activities;
using Temporalio.Exceptions;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// Activities for InstitutionActivationDispatchWorkflow — weekly activity snapshots,
/// stamping Institution.ActivatedAt, "your next setup step" nudges to institution
/// admins, onboarding-lead follow-up reminders, and the Monday digest to platform
/// staff. The activation rules are InstitutionActivationService's, shared with
/// Platform.Api's Activation page and Institution.Api's setup checklist.
/// Every read is tenant-agnostic (ignoreQueryFilters: true); tenant-scoped writes
/// set ICurrentTenantService first, same as ScheduledJobsActivities.
/// </summary>
public class InstitutionActivationActivities(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<InstitutionStaff> staffRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<StaffActivityWeek> staffActivityRepo,
    IAlumniPgRepository<InstitutionActivitySnapshot> snapshotRepo,
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<PlatformStaff> platformStaffRepo,
    IAlumniPgRepository<PlatformNotification> platformNotifRepo,
    IAlumniPgRepository<OnboardingLead> leadRepo,
    InstitutionActivationService activation,
    ICurrentTenantService currentTenant,
    ITemporalClientProvider temporalProvider,
    ISmsService smsService,
    IConfiguration configuration,
    ILogger<InstitutionActivationActivities> logger)
{
    /// <summary>No nudges before an institution has had a couple of days to settle in.</summary>
    public const int NudgeGraceDays = 2;
    /// <summary>Stop nudging after this — by then it's a conversation for a person, not an email.</summary>
    public const int NudgeWindowDays = 90;
    public const int NudgeCooldownDays = 7;
    /// <summary>The digest lists trials ending within this many days.</summary>
    public const int TrialWarningDays = 7;

    [Activity("InstitutionActivation.RecordActivitySnapshot")]
    public virtual Task RecordActivitySnapshotAsync(string institutionId) =>
        Wrap(async () =>
        {
            var now = DateTime.UtcNow;
            var weekStart = InstitutionActivationService.WeekStart(now);
            var weekEnd = weekStart.AddDays(7);

            // StaffActivityWeek is the primary record; LastLoginAt covers sessions from before it existed.
            var recordedStaff = await staffActivityRepo.CountAsync(a => a.InstitutionId == institutionId && a.WeekStart == weekStart);
            var loggedInStaff = await staffRepo.CountAsync(
                s => s.InstitutionId == institutionId && !s.IsDisabled && s.LastLoginAt >= weekStart, ignoreQueryFilters: true);

            var members = await memberRepo.GetQueryable(m => m.InstitutionId == institutionId && m.Status == "Active", ignoreQueryFilters: true)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    EverSignedIn = g.Count(m => m.LastLoginAt != null),
                    ThisWeek = g.Count(m => m.LastLoginAt >= weekStart),
                })
                .FirstOrDefaultAsync();

            var payments = await contributionRepo.GetQueryable(
                    c => c.InstitutionId == institutionId && c.Status == "Successful" && c.CreatedAt >= weekStart && c.CreatedAt < weekEnd,
                    ignoreQueryFilters: true)
                .GroupBy(_ => 1)
                .Select(g => new { Count = g.Count(), Amount = g.Sum(c => c.Amount) })
                .FirstOrDefaultAsync();

            var row = await snapshotRepo.GetOneAsync(a => a.InstitutionId == institutionId && a.WeekStart == weekStart);
            var isNew = row is null;
            row ??= new InstitutionActivitySnapshot { InstitutionId = institutionId, WeekStart = weekStart, CreatedBy = "system" };
            // Max, not overwrite: a staffer disabled mid-week still counted for the days they were active.
            row.ActiveStaffCount = Math.Max(row.ActiveStaffCount, Math.Max(recordedStaff, loggedInStaff));
            row.MemberCount = members?.Total ?? 0;
            row.MembersEverLoggedIn = members?.EverSignedIn ?? 0;
            row.MembersActiveThisWeek = Math.Max(row.MembersActiveThisWeek, members?.ThisWeek ?? 0);
            row.SuccessfulPaymentsThisWeek = payments?.Count ?? 0;
            row.AmountCollectedThisWeek = payments?.Amount ?? 0;

            if (isNew)
            {
                await snapshotRepo.AddAsync(row);
            }
            else
            {
                row.UpdatedAt = now;
                row.UpdatedBy = "system";
                await snapshotRepo.UpdateAsync(row);
            }
        }, "record activity snapshot", institutionId);

    /// <summary>
    /// Evaluates every active institution, stamps ActivatedAt the first time all
    /// criteria are met, and returns the outcome for the nudge and digest steps.
    /// </summary>
    [Activity("InstitutionActivation.EvaluateAll")]
    public virtual Task<List<ActivationRunItem>> EvaluateAllAsync() =>
        Wrap(async () =>
        {
            var now = DateTime.UtcNow;
            var institutions = (await institutionRepo.GetAllAsync(i => i.Status == "Active", ignoreQueryFilters: true)).ToList();
            var results = await activation.EvaluateAsync(institutions, now);
            var byId = institutions.ToDictionary(i => i.Id);

            var items = new List<ActivationRunItem>(results.Count);
            foreach (var r in results)
            {
                var activatedAt = r.ActivatedAt;
                if (activatedAt is null && r.AllMet)
                {
                    activatedAt = now;
                    await institutionRepo.ExecuteUpdateAsync(i => i.Id == r.InstitutionId,
                        s => s.SetProperty(i => i.ActivatedAt, now), ignoreQueryFilters: true);
                    logger.LogInformation("Institution {InstitutionId} ({Name}) reached full activation", r.InstitutionId, r.InstitutionName);
                }

                var inst = byId[r.InstitutionId];
                items.Add(new ActivationRunItem
                {
                    InstitutionId = r.InstitutionId,
                    Name = r.InstitutionName,
                    DaysLive = r.DaysLive,
                    MetCount = r.MetCount,
                    TotalCriteria = r.Criteria.Count,
                    IsActivated = activatedAt is not null,
                    IsStalled = r.IsStalled && activatedAt is null,
                    IsOverdue = r.IsOverdue && activatedAt is null,
                    NextStepKey = r.NextStepKey,
                    NextStepLabel = r.Criteria.FirstOrDefault(c => !c.Met)?.Label,
                    NextStep = r.NextStep,
                    PayoutPending = inst.PayoutStatus == "Pending",
                    LastNudgeSentAt = inst.LastActivationNudgeSentAt,
                    SetupNudgesEnabled = inst.SetupNudgesEnabled,
                    TrialEndsAt = inst.TrialEndsAt,
                });
            }
            return items;
        }, "evaluate institution activation", "all");

    /// <summary>
    /// Whether <paramref name="item"/> is due a nudge right now. Pure, so the
    /// eligibility rules are testable without a database.
    /// </summary>
    public static bool IsNudgeDue(ActivationRunItem item, DateTime now)
    {
        if (item.IsActivated || string.IsNullOrEmpty(item.NextStep) || !item.SetupNudgesEnabled) return false;
        if (item.DaysLive < NudgeGraceDays || item.DaysLive > NudgeWindowDays) return false;
        // Pending payout review is waiting on platform staff, not the institution — it shows in the digest instead.
        if (item.NextStepKey == InstitutionActivationService.Payouts && item.PayoutPending) return false;
        return item.LastNudgeSentAt is not { } last || last <= now.AddDays(-NudgeCooldownDays);
    }

    /// <summary>
    /// Emails, texts (when SMS is on and a phone is on file) and notifies in-app the
    /// institution's SuperAdmins about their next unmet step, subject to
    /// <see cref="IsNudgeDue"/>. Returns whether a nudge went out.
    /// </summary>
    [Activity("InstitutionActivation.SendNudge")]
    public virtual Task<bool> SendNudgeAsync(ActivationRunItem item) =>
        Wrap(async () =>
        {
            var now = DateTime.UtcNow;
            if (!IsNudgeDue(item, now)) return false;

            var institution = await institutionRepo.GetOneAsync(i => i.Id == item.InstitutionId, ignoreQueryFilters: true);
            if (institution is null || !institution.SetupNudgesEnabled) return false;

            var admins = (await staffRepo.GetAllAsync(
                    s => s.InstitutionId == item.InstitutionId && s.Role == "SuperAdmin" && !s.IsDisabled && s.Email != "",
                    ignoreQueryFilters: true))
                .ToList();
            if (admins.Count == 0) return false;

            // Stamp first: a retry after a partial send must not email everyone twice.
            await institutionRepo.ExecuteUpdateAsync(i => i.Id == item.InstitutionId,
                s => s.SetProperty(i => i.LastActivationNudgeSentAt, now), ignoreQueryFilters: true);

            var adminDomain = configuration["AdminPortalBaseDomain"];
            var actionUrl = string.IsNullOrWhiteSpace(adminDomain) ? string.Empty : $"https://{institution.Slug}.{adminDomain}";
            var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
            var title = $"Next step for {brandName}: {item.NextStepLabel}";
            var body = $"You've completed {item.MetCount} of {item.TotalCriteria} setup steps. {item.NextStep}";

            foreach (var admin in admins)
            {
                await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(
                    new SendEmailRequest
                    {
                        To = [new EmailContact { Email = admin.Email, Name = admin.FirstName }],
                        TemplateId = "notification",
                        TemplateVariables = new
                        {
                            first_name = admin.FirstName,
                            title,
                            body,
                            badge_label = "Setup progress",
                            action_url = actionUrl,
                            action_label = "Open your portal",
                            brand_name = brandName,
                            brand_color = institution.PrimaryColorHex,
                            brand_secondary_color = institution.SecondaryColorHex,
                            brand_logo = institution.LogoUrl,
                            pref_label = "you are an administrator of this portal",
                        },
                    },
                    $"activation nudge to {admin.Email}"), logger);
            }

            // SMS is a real per-message cost, so it follows the institution's own SMS switch.
            // (WhatsApp stays off platform-wide — see NotificationDispatchWorkflow.WhatsAppEnabled.)
            if (institution.SmsNotificationsEnabled)
            {
                var sms = SmsText($"{brandName}: next setup step, {item.NextStepLabel?.ToLowerInvariant()}. {item.NextStep}", actionUrl);
                foreach (var admin in admins.Where(a => !string.IsNullOrWhiteSpace(a.Phone)))
                {
                    try
                    {
                        if (!await smsService.SendSmsAsync(admin.Phone!, sms))
                            logger.LogWarning("Activation nudge SMS reported failure for admin {AdminId}", admin.Id);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Activation nudge SMS failed for admin {AdminId}", admin.Id);
                    }
                }
            }

            currentTenant.SetInstitutionId(item.InstitutionId);
            await notifRepo.AddRangeAsync(admins.Select(a => new Notification
            {
                RecipientId = a.Id,
                RecipientType = "Admin",
                Title = title,
                Body = body,
                Type = "SetupNudge",
                ActionUrl = actionUrl,
                CreatedBy = "system",
            }).ToList());

            logger.LogInformation("Activation nudge sent to {Count} admins of institution {InstitutionId} ({Step})",
                admins.Count, item.InstitutionId, item.NextStepLabel);
            return true;
        }, "send activation nudge", item.InstitutionId);

    /// <summary>
    /// Reminds each open lead's owner (or every SuperAdmin/Sales staffer when the lead
    /// has none) once its follow-up date arrives. Returns the number of leads reminded.
    /// </summary>
    [Activity("InstitutionActivation.SendFollowUpReminders")]
    public virtual Task<int> SendFollowUpRemindersAsync() =>
        Wrap(async () =>
        {
            var now = DateTime.UtcNow;
            var endOfToday = now.Date.AddDays(1);
            var due = (await leadRepo.GetAllAsync(l =>
                    l.NextFollowUpAt != null && l.NextFollowUpAt < endOfToday
                    && l.Status != OnboardingLeadStatuses.Approved && l.Status != OnboardingLeadStatuses.Rejected
                    && (l.FollowUpReminderSentAt == null || l.FollowUpReminderSentAt < l.NextFollowUpAt)))
                .ToList();
            if (due.Count == 0) return 0;

            var staff = (await platformStaffRepo.GetAllAsync(s => !s.IsDisabled)).ToList();
            var fallback = staff.Where(s => s.Role is "SuperAdmin" or "Sales").ToList();
            var byId = staff.ToDictionary(s => s.Id);

            var notifications = new List<PlatformNotification>();
            foreach (var lead in due)
            {
                var recipients = lead.AssigneeStaffId is { } a && byId.TryGetValue(a, out var owner) ? [owner] : fallback;
                var overdueDays = (int)(now.Date - lead.NextFollowUpAt!.Value.Date).TotalDays;
                var when = overdueDays <= 0 ? "today" : $"{overdueDays} day{(overdueDays == 1 ? "" : "s")} ago";
                notifications.AddRange(recipients.Select(r => new PlatformNotification
                {
                    RecipientStaffId = r.Id,
                    Title = $"Follow up with {lead.InstitutionName}",
                    Body = $"Follow-up with {lead.ContactName} was due {when}.",
                    Type = "LeadFollowUp",
                    RelatedEntityId = lead.Id,
                    RelatedEntityType = "OnboardingLead",
                    ActionUrl = "/onboarding-leads",
                    CreatedBy = "system",
                }));
                lead.FollowUpReminderSentAt = now;
                lead.UpdatedBy = "system";
            }

            await leadRepo.UpdateRangeAsync(due);
            if (notifications.Count > 0) await platformNotifRepo.AddRangeAsync(notifications);
            logger.LogInformation("Follow-up reminders sent for {Count} onboarding leads", due.Count);
            return due.Count;
        }, "send lead follow-up reminders", "all");

    /// <summary>Builds the Monday digest's title and body. Pure, so the wording is testable.</summary>
    public static (string Title, string Body) BuildDigest(List<ActivationRunItem> items, DateTime now)
    {
        var activated = items.Count(i => i.IsActivated);
        var title = $"Activation this week: {activated} of {items.Count} institutions active";
        var parts = new List<string> { $"{activated} of {items.Count} live institutions meet every activation criterion." };

        var overdue = items.Where(i => i.IsOverdue).OrderByDescending(i => i.DaysLive).ToList();
        var stalled = items.Where(i => i.IsStalled && !i.IsOverdue).OrderByDescending(i => i.DaysLive).ToList();
        if (overdue.Count > 0)
            parts.Add($"Past the {InstitutionActivationService.ActivationWindowDays}-day window: {Names(overdue)}");
        if (stalled.Count > 0)
            parts.Add($"Live over {InstitutionActivationService.StalledAfterDays} days and not there yet: {Names(stalled)}");
        if (overdue.Count == 0 && stalled.Count == 0)
            parts.Add("Nobody is stalled.");

        var trials = items
            .Where(i => !i.IsActivated && i.TrialEndsAt is { } t && t >= now.Date && t < now.Date.AddDays(TrialWarningDays + 1))
            .OrderBy(i => i.TrialEndsAt)
            .ToList();
        if (trials.Count > 0)
            parts.Add($"Trials ending within {TrialWarningDays} days: " + string.Join("; ", trials.Select(t => $"{t.Name} ({t.TrialEndsAt:d MMM})")) + ".");

        return (title, string.Join(" ", parts));

        static string Names(List<ActivationRunItem> list) =>
            string.Join("; ", list.Take(10).Select(s => $"{s.Name} ({s.DaysLive} days, {StuckOn(s)})"))
            + (list.Count > 10 ? $"; and {list.Count - 10} more." : ".");
    }

    /// <summary>Monday summary for SuperAdmin and Sales platform staff: progress, overdue, stalled and ending trials, by email and in-app.</summary>
    [Activity("InstitutionActivation.SendPlatformDigest")]
    public virtual Task SendPlatformDigestAsync(List<ActivationRunItem> items) =>
        Wrap(async () =>
        {
            if (items.Count == 0) return;

            var recipients = (await platformStaffRepo.GetAllAsync(s => !s.IsDisabled && (s.Role == "SuperAdmin" || s.Role == "Sales")))
                .ToList();
            if (recipients.Count == 0) return;

            var (title, body) = BuildDigest(items, DateTime.UtcNow);
            var portalUrl = configuration["PlatformPortalUrl"]?.TrimEnd('/');
            var actionUrl = string.IsNullOrWhiteSpace(portalUrl) ? string.Empty : $"{portalUrl}/activation";

            foreach (var staff in recipients)
            {
                await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(
                    new SendEmailRequest
                    {
                        To = [new EmailContact { Email = staff.Email, Name = staff.Name }],
                        TemplateId = "notification",
                        TemplateVariables = new
                        {
                            first_name = staff.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? staff.Name,
                            title,
                            body,
                            badge_label = "Weekly activation digest",
                            action_url = actionUrl,
                            action_label = "Open the scorecard",
                            pref_label = "you are SuperAdmin or Sales staff on the platform",
                        },
                    },
                    $"activation digest to {staff.Email}"), logger);
            }

            await platformNotifRepo.AddRangeAsync(recipients.Select(s => new PlatformNotification
            {
                RecipientStaffId = s.Id,
                Title = title,
                Body = body,
                Type = "ActivationDigest",
                ActionUrl = "/activation",
                CreatedBy = "system",
            }).ToList());
        }, "send platform activation digest", "all");

    private static string StuckOn(ActivationRunItem item) =>
        item.NextStepKey == InstitutionActivationService.Payouts && item.PayoutPending
            ? "payout details awaiting our review"
            : $"stuck on {item.NextStepLabel?.ToLowerInvariant()}";

    /// <summary>Single-segment-friendly SMS: link last so the message reads first, capped well under two segments' worth.</summary>
    private static string SmsText(string message, string link)
    {
        const int max = 300;
        var suffix = string.IsNullOrEmpty(link) ? string.Empty : $" {link}";
        var room = max - suffix.Length;
        var text = message.Length <= room ? message : message[..Math.Max(0, room - 1)].TrimEnd() + "…";
        return text + suffix;
    }

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }

    private static async Task Wrap(Func<Task> action, string what, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException($"Failed to {what} ({context})", ex);
        }
    }
}

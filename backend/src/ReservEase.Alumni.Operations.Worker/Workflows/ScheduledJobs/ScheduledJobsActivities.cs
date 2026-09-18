using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Activities;
using Temporalio.Exceptions;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

/// <summary>
/// One activity, one I/O call — DigestDispatchWorkflow/BirthdaySpotlightDispatchWorkflow/
/// RecurringGivingWorkflow own the sequencing and every branch/decision. Registered via
/// AddScopedActivities, so each call gets its own DI scope automatically. Every read here
/// is tenant-agnostic (ignoreQueryFilters: true, explicit institutionId in the predicate)
/// since these run on a schedule with no ambient tenant context; every write that touches
/// an ITenantScoped entity calls ICurrentTenantService.SetInstitutionId first, mirroring
/// NotificationDispatchActivities.
/// </summary>
public class ScheduledJobsActivities(
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<RecurringContribution> recurringRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<Notification> notifRepo,
    IAlumniPgRepository<ForumCategory> forumCategoryRepo,
    IAlumniPgRepository<ForumThread> forumThreadRepo,
    IAlumniPgRepository<ForumPost> forumPostRepo,
    ICurrentTenantService currentTenant,
    IPaystackService paystackService,
    PaystackConfig paystackConfig,
    ITemporalClientProvider temporalProvider,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    IConfiguration configuration,
    ILogger<ScheduledJobsActivities> logger)
{
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;
    private const int MaxFailedRecurringAttempts = 3;
    private const int MembershipReminderCooldownDays = 21;

    // ── Shared ──────────────────────────────────────────────────────────────

    [Activity("ScheduledJobs.ResolveActiveInstitutionIds")]
    public virtual Task<List<string>> ResolveActiveInstitutionIdsAsync() =>
        Wrap(async () => (await institutionRepo.GetAllAsync(i => i.Status == "Active", ignoreQueryFilters: true))
            .Select(i => i.Id).ToList(), "resolve active institutions", "all");

    // ── Digest ──────────────────────────────────────────────────────────────

    private sealed record DigestItem(string Title, string Meta, List<int>? YearGroups);
    private sealed record ContentBucket(List<DigestItem> Jobs, DigestItem? Event, DigestItem? Campaign, DigestItem? Spotlight);

    [Activity("ScheduledJobs.SendDueDigestsForInstitution")]
    public virtual Task<int> SendDueDigestsForInstitutionAsync(string institutionId) =>
        Wrap(async () =>
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (institution is null || institution.DisabledFeatures.Contains(InstitutionFeatures.Digest) || !institution.EmailNotificationsEnabled)
                return 0;

            var now = DateTime.UtcNow;
            var activeMembers = (await memberRepo.GetAllAsync(m => m.InstitutionId == institutionId && m.Status == "Active" && !string.IsNullOrEmpty(m.Email), ignoreQueryFilters: true)).ToList();
            if (activeMembers.Count == 0)
                return 0;

            var prefsByMember = (await prefRepo.GetAllAsync(p => p.InstitutionId == institutionId, ignoreQueryFilters: true)).ToDictionary(p => p.MemberId);

            var due = new List<(MemberEntity Member, NotificationPreference? Pref, string Frequency)>();
            foreach (var m in activeMembers)
            {
                if (prefsByMember.TryGetValue(m.Id, out var pref))
                {
                    if (pref.DigestFrequency == "None") continue;
                    var cutoff = pref.DigestFrequency == "Monthly" ? now.AddDays(-30) : now.AddDays(-7);
                    if (pref.LastDigestSentAt is not null && pref.LastDigestSentAt > cutoff) continue;
                    due.Add((m, pref, pref.DigestFrequency));
                }
                else
                {
                    due.Add((m, null, "Weekly"));
                }
            }

            if (due.Count == 0)
                return 0;

            var buckets = new Dictionary<string, ContentBucket>();
            var portalUrl = GetMemberPortalUrl(institution);
            var updatedPrefs = new List<NotificationPreference>();
            var newPrefs = new List<NotificationPreference>();
            var sentCount = 0;

            foreach (var (member, pref, frequency) in due)
            {
                if (!buckets.TryGetValue(frequency, out var bucket))
                {
                    bucket = await BuildContentBucketAsync(institutionId, frequency == "Monthly" ? now.AddDays(-30) : now.AddDays(-7), now);
                    buckets[frequency] = bucket;
                }

                var jobs = bucket.Jobs.Where(j => MatchesYear(j.YearGroups, member.GraduationYear)).Take(3).ToList();
                var ev = bucket.Event is not null && MatchesYear(bucket.Event.YearGroups, member.GraduationYear) ? bucket.Event : null;
                var campaign = bucket.Campaign is not null && MatchesYear(bucket.Campaign.YearGroups, member.GraduationYear) ? bucket.Campaign : null;
                var spotlight = bucket.Spotlight;

                if (jobs.Count > 0 || ev is not null || campaign is not null || spotlight is not null)
                {
                    await SendDigestEmailAsync(member, institution, frequency, portalUrl, jobs, ev, campaign, spotlight);
                    sentCount++;
                }

                if (pref is not null)
                {
                    pref.LastDigestSentAt = now;
                    pref.UpdatedBy = "system";
                    updatedPrefs.Add(pref);
                }
                else
                {
                    newPrefs.Add(new NotificationPreference { MemberId = member.Id, LastDigestSentAt = now, CreatedBy = "system" });
                }
            }

            currentTenant.SetInstitutionId(institutionId);
            if (updatedPrefs.Count > 0) await prefRepo.UpdateRangeAsync(updatedPrefs);
            if (newPrefs.Count > 0) await prefRepo.AddRangeAsync(newPrefs);

            logger.LogInformation("Digest cycle for institution {InstitutionId}: {Due} due, {Sent} sent (rest had nothing new)", institutionId, due.Count, sentCount);
            return sentCount;
        }, "send due digests for institution", institutionId);

    // ── Membership dues reminders ───────────────────────────────────────────

    [Activity("ScheduledJobs.SendDueMembershipRemindersForInstitution")]
    public virtual Task<int> SendDueMembershipRemindersForInstitutionAsync(string institutionId) =>
        Wrap(async () =>
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (institution is null || institution.DisabledFeatures.Contains(InstitutionFeatures.Contributions))
                return 0;

            var currentYear = DateTime.UtcNow.Year;
            var membershipCampaigns = (await campaignRepo.GetAllAsync(c =>
                    c.InstitutionId == institutionId && c.IsMembershipCampaign && c.MembershipYear != null && c.MembershipYear <= currentYear,
                    ignoreQueryFilters: true))
                .ToList();
            if (membershipCampaigns.Count == 0) return 0;

            var activeMembers = (await memberRepo.GetAllAsync(m => m.InstitutionId == institutionId && m.Status == "Active", ignoreQueryFilters: true)).ToList();
            if (activeMembers.Count == 0) return 0;

            var campaignIds = membershipCampaigns.Select(c => c.Id).ToHashSet();
            var paidByMember = (await contributionRepo.GetAllAsync(
                    c => c.InstitutionId == institutionId && c.Status == "Successful" && campaignIds.Contains(c.CampaignId), ignoreQueryFilters: true))
                .GroupBy(c => c.MemberId)
                .ToDictionary(g => g.Key, g => g.Select(c => c.CampaignId).ToHashSet());

            var prefsByMember = (await prefRepo.GetAllAsync(p => p.InstitutionId == institutionId, ignoreQueryFilters: true)).ToDictionary(p => p.MemberId);

            var now = DateTime.UtcNow;
            var portalUrl = GetMemberPortalUrl(institution);
            var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
            var notifications = new List<Notification>();
            var updatedPrefs = new List<NotificationPreference>();
            var newPrefs = new List<NotificationPreference>();
            var sentCount = 0;

            foreach (var member in activeMembers)
            {
                var required = membershipCampaigns.Where(c => c.MembershipYear >= member.GraduationYear).ToList();
                if (required.Count == 0) continue;

                var paidIds = paidByMember.GetValueOrDefault(member.Id) ?? new HashSet<string>();
                var unpaid = required.Where(c => !paidIds.Contains(c.Id)).OrderByDescending(c => c.MembershipYear).ToList();
                if (unpaid.Count == 0) continue;

                var pref = prefsByMember.GetValueOrDefault(member.Id);
                if (pref?.MembershipReminders == false) continue;
                if (pref?.LastMembershipReminderSentAt is not null && pref.LastMembershipReminderSentAt > now.AddDays(-MembershipReminderCooldownDays)) continue;

                var latestUnpaid = unpaid[0];
                var body = unpaid.Count > 1 || latestUnpaid.MembershipYear < currentYear
                    ? $"You have {unpaid.Count} outstanding membership due(s), including {latestUnpaid.MembershipYear}. Pay now to stay in good standing."
                    : $"Your {currentYear} membership dues are due. Pay now to stay active.";
                var actionUrl = string.IsNullOrEmpty(portalUrl) ? string.Empty : $"{portalUrl}/contributions";

                notifications.Add(new Notification
                {
                    RecipientId = member.Id,
                    RecipientType = "Member",
                    Title = "Membership Dues Reminder",
                    Body = body,
                    Type = "MembershipReminder",
                    ActionUrl = actionUrl,
                    CreatedBy = "system",
                });

                await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(
                    new SendEmailRequest
                    {
                        To = [new EmailContact { Email = member.Email, Name = member.FirstName }],
                        TemplateId = "notification",
                        TemplateVariables = new
                        {
                            first_name = member.FirstName,
                            title = "Membership Dues Reminder",
                            body,
                            badge_label = "Dues Reminder",
                            action_url = actionUrl,
                            action_label = "Pay now",
                            brand_name = brandName,
                            brand_color = institution.PrimaryColorHex,
                            brand_secondary_color = institution.SecondaryColorHex,
                            brand_logo = institution.LogoUrl,
                        },
                    },
                    $"membership reminder to {member.Email}"), logger);

                if (pref is not null)
                {
                    pref.LastMembershipReminderSentAt = now;
                    pref.UpdatedBy = "system";
                    updatedPrefs.Add(pref);
                }
                else
                {
                    newPrefs.Add(new NotificationPreference { MemberId = member.Id, LastMembershipReminderSentAt = now, CreatedBy = "system" });
                }

                sentCount++;
            }

            currentTenant.SetInstitutionId(institutionId);
            if (notifications.Count > 0) await notifRepo.AddRangeAsync(notifications);
            if (updatedPrefs.Count > 0) await prefRepo.UpdateRangeAsync(updatedPrefs);
            if (newPrefs.Count > 0) await prefRepo.AddRangeAsync(newPrefs);

            logger.LogInformation("Membership reminder cycle for institution {InstitutionId}: {Sent} reminders sent", institutionId, sentCount);
            return sentCount;
        }, "send due membership reminders for institution", institutionId);

    private static bool MatchesYear(List<int>? yearGroups, int memberYear) =>
        yearGroups is null || yearGroups.Count == 0 || yearGroups.Contains(memberYear);

    private async Task<ContentBucket> BuildContentBucketAsync(string institutionId, DateTime windowStart, DateTime now)
    {
        var jobs = (await jobRepo.GetAllAsync(j =>
                j.InstitutionId == institutionId && j.Status == "Active" && j.CommunityId == null && j.CreatedAt >= windowStart, ignoreQueryFilters: true))
            .OrderByDescending(j => j.CreatedAt)
            .Take(3)
            .Select(j => new DigestItem($"{HtmlEncode(j.Title)} at {HtmlEncode(j.Company)}", HtmlEncode(j.Location), j.YearGroups))
            .ToList();

        var upcomingEvent = (await eventRepo.GetAllAsync(e =>
                e.InstitutionId == institutionId && e.CommunityId == null && e.StartDate >= now && (e.Status == "Upcoming" || e.Status == "Ongoing"), ignoreQueryFilters: true))
            .OrderBy(e => e.StartDate)
            .FirstOrDefault();
        var eventItem = upcomingEvent is null ? null : new DigestItem(
            HtmlEncode(upcomingEvent.Title), $"{upcomingEvent.StartDate:MMMM d, yyyy} · {HtmlEncode(upcomingEvent.Venue)}", upcomingEvent.YearGroups);

        var closingCampaign = (await campaignRepo.GetAllAsync(c =>
                c.InstitutionId == institutionId && c.CommunityId == null && c.Status == CampaignStatus.Active && c.Deadline >= now && c.Deadline <= now.AddDays(14), ignoreQueryFilters: true))
            .OrderBy(c => c.Deadline)
            .FirstOrDefault();
        var campaignItem = closingCampaign is null ? null : new DigestItem(
            HtmlEncode(closingCampaign.Title), $"Closes {closingCampaign.Deadline:MMMM d, yyyy}", closingCampaign.YearGroups);

        var latestSpotlight = (await spotlightRepo.GetAllAsync(s => s.InstitutionId == institutionId && s.Status == "Approved", ignoreQueryFilters: true))
            .OrderByDescending(s => s.FeaturedMonth ?? s.CreatedAt)
            .FirstOrDefault();
        var spotlightItem = latestSpotlight is null ? null : new DigestItem(
            HtmlEncode(latestSpotlight.Title), $"Featuring {HtmlEncode(latestSpotlight.Member?.FirstName)} {HtmlEncode(latestSpotlight.Member?.LastName)}".Trim(), null);

        return new ContentBucket(jobs, eventItem, campaignItem, spotlightItem);
    }

    private async Task SendDigestEmailAsync(
        MemberEntity member, Institution institution, string frequency, string portalUrl,
        List<DigestItem> jobs, DigestItem? ev, DigestItem? campaign, DigestItem? spotlight)
    {
        var sections = new List<string>();
        if (jobs.Count > 0) sections.Add(BuildSection("New jobs", jobs));
        if (ev is not null) sections.Add(BuildSection("Upcoming event", [ev]));
        if (campaign is not null) sections.Add(BuildSection("Campaign closing soon", [campaign]));
        if (spotlight is not null) sections.Add(BuildSection("Alumni spotlight", [spotlight]));

        var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;

        await temporalProvider.EnqueueNotificationAsync(
            NotificationRequest.Email(
                new SendEmailRequest
                {
                    To = [new EmailContact { Email = member.Email, Name = $"{member.FirstName} {member.LastName}".Trim() }],
                    TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.Digest) ? "digest" : mailtrapConfig.Templates.Digest,
                    TemplateVariables = new
                    {
                        member_first_name = member.FirstName,
                        digest_period_label = frequency,
                        digest_frequency_label = frequency.ToLowerInvariant(),
                        digest_sections_html = string.Join("", sections),
                        portal_url = portalUrl,
                        preferences_url = $"{portalUrl}/profile",
                        brand_name = brandName,
                        brand_color = institution.PrimaryColorHex,
                        brand_secondary_color = institution.SecondaryColorHex,
                        brand_logo = institution.LogoUrl,
                    },
                },
                $"{frequency.ToLowerInvariant()} digest to {member.Email}"),
            logger);
    }

    private static string BuildSection(string title, List<DigestItem> items)
    {
        var itemsHtml = string.Join("", items.Select(i =>
            $"<div class=\"item\"><p class=\"item-title\">{i.Title}</p><p class=\"item-meta\">{i.Meta}</p></div>"));
        return $"<div class=\"section\"><p class=\"section-title\">{HtmlEncode(title)}</p>{itemsHtml}</div>";
    }

    private static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private string GetMemberPortalUrl(Institution institution)
    {
        var domain = configuration["MemberPortalBaseDomain"];
        return string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{institution.Slug}.{domain}";
    }

    // ── Birthday spotlight ──────────────────────────────────────────────────

    [Activity("ScheduledJobs.ResolveBirthdaySpotlightEligibleInstitutionIds")]
    public virtual Task<List<string>> ResolveBirthdaySpotlightEligibleInstitutionIdsAsync() =>
        Wrap(async () => (await institutionRepo.GetQueryable(i => i.Status == "Active", ignoreQueryFilters: true)
                .Select(i => new { i.Id, i.DisabledFeatures })
                .ToListAsync())
            .Where(i => !i.DisabledFeatures.Contains(InstitutionFeatures.BirthdaySpotlight))
            .Select(i => i.Id)
            .ToList(), "resolve birthday spotlight eligible institutions", "all");

    [Activity("ScheduledJobs.LoadTodaysCelebrants")]
    public virtual Task<List<BirthdayCelebrant>> LoadTodaysCelebrantsAsync(string institutionId, DateTime today) =>
        Wrap(async () =>
        {
            var alreadyRanToday = await spotlightRepo.GetOneAsync(
                s => s.InstitutionId == institutionId && s.Type == "Birthday" && s.FeaturedMonth == today, ignoreQueryFilters: true) is not null;
            if (alreadyRanToday) return [];

            return (await memberRepo.GetAllAsync(m =>
                    m.InstitutionId == institutionId && m.DateOfBirth != null && m.Status == "Active", ignoreQueryFilters: true))
                .Where(m => m.DateOfBirth!.Value.Month == today.Month && m.DateOfBirth.Value.Day == today.Day)
                .OrderByDescending(m => m.FirstName)
                .Select(m => new BirthdayCelebrant
                {
                    Id = m.Id,
                    FirstName = m.FirstName,
                    LastName = m.LastName,
                    Email = m.Email,
                    ProfilePictureUrl = m.ProfilePictureUrl,
                    MemberNumber = m.MemberNumber,
                })
                .ToList();
        }, "load today's celebrants", institutionId);

    private const string CelebrationsCategoryName = "Celebrations";

    [Activity("ScheduledJobs.CreateBirthdaySpotlight")]
    public virtual Task CreateBirthdaySpotlightAsync(string institutionId, List<BirthdayCelebrant> celebrants, string title, string story, DateTime today) =>
        Wrap(async () =>
        {
            currentTenant.SetInstitutionId(institutionId);
            var snapshots = celebrants.Select(c => new MemberSnapshot
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                ProfilePictureUrl = c.ProfilePictureUrl,
                MemberNumber = c.MemberNumber,
            }).ToList();

            var spotlight = new Spotlight
            {
                InstitutionId = institutionId,
                MemberId = celebrants[0].Id,
                Member = snapshots[0],
                Type = "Birthday",
                MemberIds = celebrants.Select(c => c.Id).ToList(),
                Members = snapshots,
                Title = title,
                Story = story,
                Status = "Approved",
                FeaturedMonth = today,
                CreatedBy = "system",
            };
            await spotlightRepo.AddAsync(spotlight);

            // One shoutout thread per celebrant, posted as the celebrant
            // themselves (reads naturally — "it's my birthday!" — inviting
            // classmates to reply underneath) so the notification below has
            // somewhere concrete to send people, instead of just announcing
            // the birthday with nothing to do about it.
            var category = await GetOrCreateCelebrationsCategoryAsync(institutionId);
            var threadIds = new List<string>();

            foreach (var celebrant in celebrants)
            {
                var authorSnap = snapshots.First(s => s.Id == celebrant.Id);
                var thread = new ForumThread
                {
                    InstitutionId = institutionId,
                    CategoryId = category.Id,
                    Category = new ForumCategorySnapshot { Id = category.Id, Name = category.Name, Description = category.Description, SortOrder = category.SortOrder },
                    Title = $"🎂 It's my birthday, {celebrant.FirstName}!",
                    AuthorId = celebrant.Id,
                    Author = authorSnap,
                    CreatedBy = "system",
                };
                await forumThreadRepo.AddAsync(thread);

                await forumPostRepo.AddAsync(new ForumPost
                {
                    InstitutionId = institutionId,
                    ThreadId = thread.Id,
                    Thread = new ForumThreadSnapshot { Id = thread.Id, Title = thread.Title },
                    AuthorId = celebrant.Id,
                    Author = authorSnap,
                    Content = $"Today's my birthday! Drop a wish below 🎉",
                    CreatedBy = "system",
                });

                threadIds.Add(thread.Id);

                await temporalProvider.EnqueueNotificationAsync(
                    NotificationRequest.BirthdayShoutout(institutionId, celebrant.Id, celebrant.FirstName, thread.Id), logger);
            }

            spotlight.ForumThreadIds = threadIds;
            await spotlightRepo.UpdateAsync(spotlight);
        }, "create birthday spotlight", institutionId);

    /// <summary>Self-healing: most institutions won't have manually created a "Celebrations"
    /// forum category, so this creates one the first time a birthday shoutout needs it,
    /// rather than requiring an admin to set that up before the feature works at all.</summary>
    private async Task<ForumCategory> GetOrCreateCelebrationsCategoryAsync(string institutionId)
    {
        var existing = await forumCategoryRepo.GetOneAsync(c => c.InstitutionId == institutionId && c.Name == CelebrationsCategoryName, ignoreQueryFilters: true);
        if (existing is not null) return existing;

        var category = new ForumCategory
        {
            InstitutionId = institutionId,
            Name = CelebrationsCategoryName,
            Description = "Birthdays, shoutouts, and good news from the community.",
            CreatedBy = "system",
        };
        await forumCategoryRepo.AddAsync(category);
        return category;
    }

    // ── Recurring giving ────────────────────────────────────────────────────

    [Activity("ScheduledJobs.LoadInstitutionForRecurringGiving")]
    public virtual Task<RecurringInstitutionInfo?> LoadInstitutionForRecurringGivingAsync(string institutionId) =>
        Wrap(async () =>
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
            if (institution is null || institution.DisabledFeatures.Contains(InstitutionFeatures.RecurringGiving))
                return null;

            return new RecurringInstitutionInfo
            {
                Id = institution.Id,
                PaystackSubaccountCode = institution.PaystackSubaccountCode,
                PlatformFeePercentage = institution.PlatformFeePercentage,
                PlatformFeeFlatThreshold = institution.PlatformFeeFlatThreshold,
                PlatformFeeFlatAmount = institution.PlatformFeeFlatAmount,
            };
        }, "load institution for recurring giving", institutionId);

    [Activity("ScheduledJobs.ResolveDueRecurringGifts")]
    public virtual Task<List<RecurringGiftDue>> ResolveDueRecurringGiftsAsync(string institutionId) =>
        Wrap(async () =>
        {
            var now = DateTime.UtcNow;
            var due = await recurringRepo.GetAllAsync(
                r => r.InstitutionId == institutionId && r.Status == "Active" && r.NextChargeDate <= now, ignoreQueryFilters: true);

            var result = new List<RecurringGiftDue>();
            foreach (var r in due)
            {
                var member = await memberRepo.GetOneAsync(m => m.Id == r.MemberId, ignoreQueryFilters: true);
                var email = member?.Email ?? r.Member?.Email;
                if (string.IsNullOrWhiteSpace(email))
                {
                    // Mirrors the old processor: no email on file means we can't charge — pause immediately rather than surface it as a "due" item.
                    r.Status = "Paused";
                    r.UpdatedBy = "system";
                    await recurringRepo.UpdateAsync(r);
                    continue;
                }

                result.Add(new RecurringGiftDue
                {
                    Id = r.Id,
                    MemberId = r.MemberId,
                    MemberEmail = email,
                    MemberFirstName = member?.FirstName ?? r.Member?.FirstName,
                    Member = r.Member,
                    CampaignId = r.CampaignId,
                    Campaign = r.Campaign,
                    Amount = r.Amount,
                    AuthorizationCode = r.AuthorizationCode,
                    FailedAttemptCount = r.FailedAttemptCount,
                });
            }
            return result;
        }, "resolve due recurring gifts", institutionId);

    [Activity("ScheduledJobs.LoadCampaignForRecurring")]
    public virtual Task<RecurringCampaignInfo?> LoadCampaignForRecurringAsync(string campaignId) =>
        Wrap(async () =>
        {
            var campaign = await campaignRepo.GetOneAsync(c => c.Id == campaignId, ignoreQueryFilters: true);
            return campaign is null ? null : new RecurringCampaignInfo
            {
                Id = campaign.Id,
                InstitutionId = campaign.InstitutionId,
                Title = campaign.Title,
                IsActive = campaign.Status == CampaignStatus.Active,
                YearGroups = campaign.YearGroups,
            };
        }, "load campaign for recurring giving", campaignId);

    [Activity("ScheduledJobs.GetPaystackGatewayFeeConfig")]
    public virtual Task<PaystackGatewayFeeConfig> GetPaystackGatewayFeeConfigAsync() =>
        Wrap(() => Task.FromResult(new PaystackGatewayFeeConfig
        {
            GatewayFeePercentage = paystackConfig.GatewayFeePercentage,
            GatewayFixedFeeSubunit = paystackConfig.GatewayFixedFeeSubunit,
            GatewayFeeCapSubunit = paystackConfig.GatewayFeeCapSubunit,
            GatewayFeeSafetyBufferSubunit = paystackConfig.GatewayFeeSafetyBufferSubunit,
        }), "get paystack gateway fee config", "static");

    [Activity("ScheduledJobs.EnqueueContributionConfirmedNotification")]
    public virtual Task EnqueueContributionConfirmedNotificationAsync(NotificationRequest request) =>
        Wrap(() => temporalProvider.EnqueueNotificationAsync(request, logger), "enqueue contribution confirmed notification", request.ContributionId ?? "unknown");

    [Activity("ScheduledJobs.ResolveBatchSubaccount")]
    public virtual Task<string?> ResolveBatchSubaccountAsync(string institutionId, int year) =>
        Wrap(async () =>
        {
            var batch = await batchRepo.GetOneAsync(b => b.InstitutionId == institutionId && b.Year == year, ignoreQueryFilters: true);
            return batch is { PayoutStatus: "Approved", UseInstitutionAccount: false } && !string.IsNullOrEmpty(batch.PaystackSubaccountCode)
                ? batch.PaystackSubaccountCode
                : null;
        }, "resolve batch subaccount", institutionId);

    [Activity("ScheduledJobs.PauseRecurringGiving")]
    public virtual Task PauseRecurringGivingAsync(string recurringId, string reason) =>
        Wrap(async () =>
        {
            var recurring = await recurringRepo.GetOneAsync(r => r.Id == recurringId, ignoreQueryFilters: true);
            if (recurring is null) return;
            recurring.Status = "Paused";
            recurring.UpdatedBy = "system";
            await recurringRepo.UpdateAsync(recurring);
            logger.LogInformation("Paused recurring gift {RecurringId}: {Reason}", recurringId, reason);
        }, "pause recurring giving", recurringId);

    [Activity("ScheduledJobs.ChargeRecurringGiving")]
    public virtual Task<ChargeAuthorizationResponse> ChargeRecurringGivingAsync(ChargeAuthorizationRequest request) =>
        Wrap(() => paystackService.ChargeAuthorizationAsync(request), "charge recurring giving", request.Reference);

    [Activity("ScheduledJobs.CreateContributionFromRecurring")]
    public virtual Task<Contribution> CreateContributionFromRecurringAsync(Contribution contribution) =>
        Wrap(async () => { await contributionRepo.AddAsync(contribution); return contribution; }, "create contribution from recurring giving", contribution.RecurringContributionId ?? contribution.Id);

    [Activity("ScheduledJobs.UpdateCampaignTotalsForRecurring")]
    public virtual Task UpdateCampaignTotalsForRecurringAsync(string campaignId, decimal amount) =>
        Wrap(async () =>
        {
            var campaign = await campaignRepo.GetOneAsync(c => c.Id == campaignId, ignoreQueryFilters: true);
            if (campaign is null) return;
            campaign.CollectedAmount += amount;
            campaign.PaidCount += 1;
            await campaignRepo.UpdateAsync(campaign);
        }, "update campaign totals for recurring giving", campaignId);

    [Activity("ScheduledJobs.MarkRecurringChargeSuccessful")]
    public virtual Task MarkRecurringChargeSuccessfulAsync(string recurringId, DateTime chargedAt, DateTime nextChargeDate) =>
        Wrap(async () =>
        {
            var recurring = await recurringRepo.GetOneAsync(r => r.Id == recurringId, ignoreQueryFilters: true);
            if (recurring is null) return;
            recurring.LastChargeAt = chargedAt;
            recurring.LastChargeStatus = "Successful";
            recurring.FailedAttemptCount = 0;
            recurring.NextChargeDate = nextChargeDate;
            recurring.UpdatedBy = "system";
            await recurringRepo.UpdateAsync(recurring);
        }, "mark recurring charge successful", recurringId);

    [Activity("ScheduledJobs.MarkRecurringChargeFailed")]
    public virtual Task MarkRecurringChargeFailedAsync(string recurringId, DateTime failedAt, string failureReason, string newStatus, int newAttemptCount, DateTime? nextChargeDate) =>
        Wrap(async () =>
        {
            var recurring = await recurringRepo.GetOneAsync(r => r.Id == recurringId, ignoreQueryFilters: true);
            if (recurring is null) return;
            recurring.LastChargeAt = failedAt;
            recurring.LastChargeStatus = $"Failed: {failureReason}";
            recurring.FailedAttemptCount = newAttemptCount;
            recurring.Status = newStatus;
            // Null means "stopped" — NextChargeDate is left as-is, matching the retired
            // RecurringGivingProcessor which never touched it once Status became Failed.
            if (nextChargeDate.HasValue) recurring.NextChargeDate = nextChargeDate.Value;
            recurring.UpdatedBy = "system";
            await recurringRepo.UpdateAsync(recurring);
        }, "mark recurring charge failed", recurringId);

    [Activity("ScheduledJobs.CreateRecurringGivingStoppedNotification")]
    public virtual Task CreateRecurringGivingStoppedNotificationAsync(string institutionId, string memberId, string campaignTitle, string recurringId) =>
        Wrap(() =>
        {
            currentTenant.SetInstitutionId(institutionId);
            return notifRepo.AddAsync(new Notification
            {
                RecipientId = memberId,
                RecipientType = "Member",
                Title = "Recurring Gift Stopped",
                Body = $"We couldn't charge your monthly gift to \"{campaignTitle}\" after {MaxFailedRecurringAttempts} attempts, so it's been stopped. Set up a new one anytime from your giving history.",
                Type = "ContributionRejected",
                RelatedEntityId = recurringId,
                RelatedEntityType = "RecurringContribution",
                CreatedBy = "system",
            });
        }, "create recurring giving stopped notification", recurringId);

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

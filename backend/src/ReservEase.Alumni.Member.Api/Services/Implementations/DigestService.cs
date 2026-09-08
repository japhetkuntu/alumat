using System.Net;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Member.Api.Actors;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Builds and sends the "here's what happened" re-engagement digest — the
/// standing lever every alumni platform uses to pull members back before
/// their next dues cycle or campaign deadline. Content is intentionally kept
/// to institution-wide items (CommunityId == null) rather than joining
/// through CommunityMembership per recipient — a full community-scoped
/// digest would mean a per-member membership lookup inside a cross-tenant
/// background loop, which doesn't scale the way a single per-institution
/// content pull does. Most jobs/events/campaigns are institution-wide anyway.
/// </summary>
public class DigestService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ICurrentTenantService currentTenant,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    INotificationActor notificationActor,
    IConfiguration configuration,
    ILogger<DigestService> logger) : IDigestService
{
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;

    private sealed record DigestItem(string Title, string Meta, List<int>? YearGroups);

    private sealed record ContentBucket(List<DigestItem> Jobs, DigestItem? Event, DigestItem? Campaign, DigestItem? Spotlight);

    public async Task<int> SendDueDigestsAsync()
    {
        if (string.IsNullOrEmpty(currentTenant.InstitutionId))
            return 0;

        var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        if (institution is null)
            return 0;

        var now = DateTime.UtcNow;
        var activeMembers = (await memberRepo.GetAllAsync(m => m.Status == "Active" && !string.IsNullOrEmpty(m.Email))).ToList();
        if (activeMembers.Count == 0)
            return 0;

        var prefsByMember = (await prefRepo.GetAllAsync()).ToDictionary(p => p.MemberId);

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
                // No preference row yet = default schedule (Weekly, never sent) — same
                // "absent row = default" convention NotificationDispatcher uses elsewhere.
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
                bucket = await BuildContentBucketAsync(frequency == "Monthly" ? now.AddDays(-30) : now.AddDays(-7), now, portalUrl);
                buckets[frequency] = bucket;
            }

            var jobs = bucket.Jobs.Where(j => MatchesYear(j.YearGroups, member.GraduationYear)).Take(3).ToList();
            var ev = bucket.Event is not null && MatchesYear(bucket.Event.YearGroups, member.GraduationYear) ? bucket.Event : null;
            var campaign = bucket.Campaign is not null && MatchesYear(bucket.Campaign.YearGroups, member.GraduationYear) ? bucket.Campaign : null;
            var spotlight = bucket.Spotlight;

            if (jobs.Count == 0 && ev is null && campaign is null && spotlight is null)
            {
                // Nothing to say this cycle — still mark as "checked" so the
                // schedule advances, otherwise a quiet institution would
                // re-evaluate (and eventually spam) this member every run.
            }
            else
            {
                SendDigestEmail(member, institution, frequency, portalUrl, jobs, ev, campaign, spotlight);
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
                newPrefs.Add(new NotificationPreference
                {
                    MemberId = member.Id,
                    LastDigestSentAt = now,
                    CreatedBy = "system",
                });
            }
        }

        if (updatedPrefs.Count > 0) await prefRepo.UpdateRangeAsync(updatedPrefs);
        if (newPrefs.Count > 0) await prefRepo.AddRangeAsync(newPrefs);

        logger.LogInformation(
            "Digest cycle for institution {InstitutionId}: {Due} due, {Sent} sent (rest had nothing new)",
            institution.Id, due.Count, sentCount);

        return sentCount;
    }

    private static bool MatchesYear(List<int>? yearGroups, int memberYear) =>
        yearGroups is null || yearGroups.Count == 0 || yearGroups.Contains(memberYear);

    private async Task<ContentBucket> BuildContentBucketAsync(DateTime windowStart, DateTime now, string portalUrl)
    {
        var jobs = (await jobRepo.GetAllAsync(j =>
                j.Status == "Active" && j.CommunityId == null && j.CreatedAt >= windowStart))
            .OrderByDescending(j => j.CreatedAt)
            .Take(3)
            .Select(j => new DigestItem(
                $"{HtmlEncode(j.Title)} at {HtmlEncode(j.Company)}",
                HtmlEncode(j.Location),
                j.YearGroups))
            .ToList();

        var upcomingEvent = (await eventRepo.GetAllAsync(e =>
                e.CommunityId == null && e.StartDate >= now && (e.Status == "Upcoming" || e.Status == "Ongoing")))
            .OrderBy(e => e.StartDate)
            .FirstOrDefault();
        var eventItem = upcomingEvent is null ? null : new DigestItem(
            HtmlEncode(upcomingEvent.Title),
            $"{upcomingEvent.StartDate:MMMM d, yyyy} · {HtmlEncode(upcomingEvent.Venue)}",
            upcomingEvent.YearGroups);

        var closingCampaign = (await campaignRepo.GetAllAsync(c =>
                c.CommunityId == null && c.Status == CampaignStatus.Active && c.Deadline >= now && c.Deadline <= now.AddDays(14)))
            .OrderBy(c => c.Deadline)
            .FirstOrDefault();
        var campaignItem = closingCampaign is null ? null : new DigestItem(
            HtmlEncode(closingCampaign.Title),
            $"Closes {closingCampaign.Deadline:MMMM d, yyyy}",
            closingCampaign.YearGroups);

        var latestSpotlight = (await spotlightRepo.GetAllAsync(s => s.Status == "Approved"))
            .OrderByDescending(s => s.FeaturedMonth ?? s.CreatedAt)
            .FirstOrDefault();
        var spotlightItem = latestSpotlight is null ? null : new DigestItem(
            HtmlEncode(latestSpotlight.Title),
            $"Featuring {HtmlEncode(latestSpotlight.Member?.FirstName)} {HtmlEncode(latestSpotlight.Member?.LastName)}".Trim(),
            null);

        return new ContentBucket(jobs, eventItem, campaignItem, spotlightItem);
    }

    private void SendDigestEmail(
        MemberEntity member, Institution institution, string frequency, string portalUrl,
        List<DigestItem> jobs, DigestItem? ev, DigestItem? campaign, DigestItem? spotlight)
    {
        var sections = new List<string>();
        if (jobs.Count > 0) sections.Add(BuildSection("New jobs", jobs));
        if (ev is not null) sections.Add(BuildSection("Upcoming event", [ev]));
        if (campaign is not null) sections.Add(BuildSection("Campaign closing soon", [campaign]));
        if (spotlight is not null) sections.Add(BuildSection("Alumni spotlight", [spotlight]));

        var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;

        notificationActor.Tell(new SendEmailCommand(
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
            $"{frequency.ToLowerInvariant()} digest to {member.Email}"));
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
        var domain = configuration["PlatformBaseDomain"];
        return string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{institution.Slug}.{domain}";
    }
}

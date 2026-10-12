using System.Security.Cryptography;
using System.Text;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// Connecting WhatsApp groups to the institution and posting to them. Everything is explicit: a group joins only through a code
/// an administrator generated and a confirmation they give; a post goes out only after an administrator has seen the exact text
/// and pressed publish. Nothing is sent automatically, and nothing here reads or stores group messages.
/// </summary>
public class WhatsAppGroupService(
    IAlumniPgRepository<PostgresDb.Sdk.Entities.Institution> institutionRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<NotificationPreference> prefRepo,
    IAlumniPgRepository<Referral> referralRepo,
    ICurrentTenantService tenant,
    IInstitutionAuditLogService auditLog,
    ITemporalClientProvider temporal,
    IConfiguration configuration,
    ILogger<WhatsAppGroupService> logger,
    ReservEase.Alumni.PostgresDb.Sdk.Services.IPlatformWhatsAppSwitch? platformSwitch = null) : IWhatsAppGroupService
{
    private const int MaxGroups = 10;
    private const int MaxMessageLength = 1000;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromHours(24);
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>The channel is set up AND the platform administrator has not switched the bot off.</summary>
    private async Task<bool> PlatformEnabledAsync()
        => configuration.GetValue("WaSenderConfig:Enabled", false) && (platformSwitch is null || await platformSwitch.IsOnAsync());
    private string? AssistantNumber => configuration["WaSenderConfig:AssistantNumber"] is { Length: > 0 } n ? n : null;

    private async Task<PostgresDb.Sdk.Entities.Institution?> LoadAsync()
        => tenant.InstitutionId is null ? null : await institutionRepo.GetOneAsync(i => i.Id == tenant.InstitutionId, ignoreQueryFilters: true);

    private static IApiResponse<T> NoInstitution<T>() => ApiResponseExtensions.ToNotFoundApiResponse<T>("Institution not found");

    // ── Adoption ─────────────────────────────────────────────────────────

    public async Task<IApiResponse<WhatsAppAdoptionDto>> GetAdoptionAsync()
    {
        try
        {
            var institution = await LoadAsync();
            if (institution is null) return NoInstitution<WhatsAppAdoptionDto>();

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var active = await memberRepo.CountAsync(m => m.Status == "Active");
            var connected = await memberRepo.CountAsync(m => m.Status == "Active" && m.PhoneVerifiedAt != null);
            var connectedThisMonth = await memberRepo.CountAsync(m => m.Status == "Active" && m.PhoneVerifiedAt != null && m.PhoneVerifiedAt >= monthStart);
            var alertsOn = await prefRepo.CountAsync(p => p.WhatsAppAlerts);

            // The last six months, oldest first, with months that had no messages shown as zero so the table has no holes.
            var months = Enumerable.Range(0, 6).Select(i => DateTime.UtcNow.AddMonths(-i)).Select(MessagingUsageKeys.For).Reverse()
                .Select(k =>
                {
                    institution.MessagingUsage.TryGetValue(k, out var u);
                    return new WhatsAppUsageMonthDto(k, u?.WhatsAppDelivered ?? 0, u?.SmsSent ?? 0, u?.SmsAfterWhatsAppFailure ?? 0);
                }).ToList();

            var since = DateTime.UtcNow.AddDays(-90);
            var referrals = (await referralRepo.GetAllAsync(r => r.CreatedAt >= since)).ToList();
            var channels = referrals
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Channel) ? "other" : r.Channel!.ToLowerInvariant())
                .Select(g => new WhatsAppReferralChannelDto(g.Key, g.Count(), g.Count(r => r.Status is "Registered" or "MembershipPaid")))
                .OrderByDescending(c => c.Invited).ToList();

            return new WhatsAppAdoptionDto(active, connected, alertsOn, connectedThisMonth, months, channels).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Loading WhatsApp adoption failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppAdoptionDto>("Failed to load WhatsApp adoption");
        }
    }

    // ── Groups ───────────────────────────────────────────────────────────

    public async Task<IApiResponse<WhatsAppOverviewDto>> GetOverviewAsync()
    {
        try
        {
            var institution = await LoadAsync();
            if (institution is null) return NoInstitution<WhatsAppOverviewDto>();

            var communities = (await communityRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
            var link = AssistantNumber is null ? null : $"https://wa.me/{AssistantNumber}?text=start%20{institution.Slug}";
            var groups = institution.WhatsAppGroups.OrderByDescending(g => g.CreatedAt).Select(g => ToDto(g, communities)).ToList();
            return new WhatsAppOverviewDto(await PlatformEnabledAsync(), AssistantNumber, link, groups, [.. WhatsAppGroupCommands.All]).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Loading WhatsApp overview failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppOverviewDto>("Failed to load WhatsApp settings");
        }
    }

    public async Task<IApiResponse<WhatsAppGroupDto>> CreateAsync(CreateWhatsAppGroupRequest request, AuthData admin)
    {
        try
        {
            var institution = await LoadAsync();
            if (institution is null) return NoInstitution<WhatsAppGroupDto>();

            var name = request.Name?.Trim() ?? "";
            if (name.Length < 2 || name.Length > 80)
                return ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppGroupDto>("Give the group a name of 2 to 80 characters.");
            if (institution.WhatsAppGroups.Count >= MaxGroups)
                return ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppGroupDto>($"You can connect up to {MaxGroups} groups.");

            string? communityName = null;
            if (!string.IsNullOrWhiteSpace(request.CommunityId))
            {
                var community = await communityRepo.GetByIdAsync(request.CommunityId);
                if (community is null) return ApiResponseExtensions.ToNotFoundApiResponse<WhatsAppGroupDto>("Community not found");
                communityName = community.Name;
            }

            var link = new WhatsAppGroupLink
            {
                Name = name,
                CommunityId = string.IsNullOrWhiteSpace(request.CommunityId) ? null : request.CommunityId,
                Status = WhatsAppGroupStatuses.AwaitingGroup,
                Code = NewCode(),
                CodeExpiresAt = DateTime.UtcNow.Add(CodeLifetime),
                CreatedBy = admin.Id,
            };
            institution.WhatsAppGroups = [.. institution.WhatsAppGroups, link];
            await institutionRepo.UpdateAsync(institution);
            await auditLog.LogAsync(admin, "WhatsApp Group Link Started", name);

            return ToDto(link, communityName is null ? [] : new() { [link.CommunityId!] = communityName }).ToCreatedApiResponse("Group link created.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Creating WhatsApp group link failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppGroupDto>("Failed to create the group link");
        }
    }

    public async Task<IApiResponse<WhatsAppGroupDto>> ConfirmAsync(string groupId, AuthData admin)
    {
        try
        {
            var (institution, link) = await FindAsync(groupId);
            if (institution is null || link is null) return ApiResponseExtensions.ToNotFoundApiResponse<WhatsAppGroupDto>("Group not found");
            if (link.Status != WhatsAppGroupStatuses.Detected || string.IsNullOrEmpty(link.GroupJid))
                return ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppGroupDto>("This group hasn't been detected yet. Post the code in the WhatsApp group first.");

            link.Status = WhatsAppGroupStatuses.Active;
            link.LinkedAt = DateTime.UtcNow;
            link.ConfirmedBy = admin.Id;
            await Save(institution);
            await auditLog.LogAsync(admin, "WhatsApp Group Connected", link.Name);
            return ToDto(link, await CommunityNames()).ToOkApiResponse("Group connected.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Confirming WhatsApp group failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppGroupDto>("Failed to connect the group");
        }
    }

    public async Task<IApiResponse<WhatsAppGroupDto>> UpdateAsync(string groupId, UpdateWhatsAppGroupRequest request, AuthData admin)
    {
        try
        {
            var (institution, link) = await FindAsync(groupId);
            if (institution is null || link is null) return ApiResponseExtensions.ToNotFoundApiResponse<WhatsAppGroupDto>("Group not found");

            var commands = (request.AllowedCommands ?? []).Select(c => c.Trim().TrimStart('!').ToLowerInvariant()).Where(c => c.Length > 0).Distinct().ToList();
            if (commands.Any(c => !WhatsAppGroupCommands.All.Contains(c)))
                return ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppGroupDto>("One of those commands isn't one the assistant knows.");
            var name = request.Name?.Trim();
            if (!string.IsNullOrEmpty(name) && (name.Length < 2 || name.Length > 80))
                return ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppGroupDto>("Give the group a name of 2 to 80 characters.");

            if (!string.IsNullOrEmpty(name)) link.Name = name;
            link.AssistantEnabled = request.AssistantEnabled;
            link.PublishingEnabled = request.PublishingEnabled;
            if (request.InviteLink is not null)
            {
                var invite = request.InviteLink.Trim();
                if (invite.Length > 0 && !IsGroupInviteLink(invite))
                    return ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppGroupDto>("That doesn't look like a WhatsApp group invite link. It should start with https://chat.whatsapp.com/");
                link.InviteLink = invite.Length == 0 ? null : invite;
            }
            // Every command ticked is the same as none restricted.
            link.AllowedCommands = commands.Count == WhatsAppGroupCommands.All.Count ? [] : commands;
            await Save(institution);
            await auditLog.LogAsync(admin, "WhatsApp Group Updated", link.Name);
            return ToDto(link, await CommunityNames()).ToOkApiResponse("Group updated.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Updating WhatsApp group failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppGroupDto>("Failed to update the group");
        }
    }

    public async Task<IApiResponse<object>> DeleteAsync(string groupId, AuthData admin)
    {
        try
        {
            var (institution, link) = await FindAsync(groupId);
            if (institution is null || link is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Group not found");

            institution.WhatsAppGroups = institution.WhatsAppGroups.Where(g => g.Id != groupId).ToList();
            await institutionRepo.UpdateAsync(institution);
            await auditLog.LogAsync(admin, "WhatsApp Group Disconnected", link.Name);
            return new object().ToOkApiResponse("Group disconnected.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Disconnecting WhatsApp group failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to disconnect the group");
        }
    }

    // ── Publishing ───────────────────────────────────────────────────────

    public async Task<IApiResponse<WhatsAppPublishPreviewDto>> PreviewAsync(WhatsAppPublishRequest request)
    {
        try
        {
            var built = await BuildAsync(request);
            return built.Error is not null
                ? ApiResponseExtensions.ToBadRequestApiResponse<WhatsAppPublishPreviewDto>(built.Error)
                : new WhatsAppPublishPreviewDto(built.Title!, built.Text!, built.Link!).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Building WhatsApp preview failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<WhatsAppPublishPreviewDto>("Failed to prepare the message");
        }
    }

    public async Task<IApiResponse<object>> PublishAsync(WhatsAppPublishRequest request, AuthData admin)
    {
        try
        {
            if (!await PlatformEnabledAsync())
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("WhatsApp isn't switched on for the platform yet.");

            var built = await BuildAsync(request);
            if (built.Error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<object>(built.Error);

            // The administrator may have edited the preview; whatever they confirmed is what is sent. The exact link always stays.
            var text = string.IsNullOrWhiteSpace(request.Text) ? built.Text! : request.Text.Trim();
            if (text.Length > MaxMessageLength)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Keep the message under {MaxMessageLength} characters.");
            if (!text.Contains(built.Link!, StringComparison.Ordinal)) text = $"{text}\n{built.Link}";

            var queued = await temporal.EnqueueWhatsAppAsync(
                new WhatsAppSendRequest(built.Group!.GroupJid!, text, "admin-publish"), $"pub-{Guid.NewGuid():N}", logger);
            if (!queued)
                return ApiResponseExtensions.ToServerErrorApiResponse<object>("We couldn't queue the message right now. Please try again in a moment.");

            await auditLog.LogAsync(admin, "Posted To WhatsApp Group", $"{built.Title} → {built.Group.Name}");
            return new object().ToOkApiResponse("Queued. It will appear in the group shortly.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Publishing to a WhatsApp group failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to post to the group");
        }
    }

    private sealed record Built(string? Title, string? Text, string? Link, WhatsAppGroupLink? Group, string? Error);

    private async Task<Built> BuildAsync(WhatsAppPublishRequest request)
    {
        var institution = await LoadAsync();
        if (institution is null) return new(null, null, null, null, "Institution not found");

        var group = institution.WhatsAppGroups.FirstOrDefault(g => g.Id == request.GroupId);
        if (group is null) return new(null, null, null, null, "That group isn't connected.");
        if (group.Status != WhatsAppGroupStatuses.Active || string.IsNullOrEmpty(group.GroupJid))
            return new(null, null, null, null, "Confirm this group before posting to it.");
        if (!group.PublishingEnabled) return new(null, null, null, null, "Posting is switched off for this group.");

        var portal = MemberPortalLinks.Url(institution.Slug, institution.CustomDomain, configuration["MemberPortalBaseDomain"]);
        if (portal is null) return new(null, null, null, null, "The member portal address isn't configured, so a link can't be built.");

        // A group has people in it we cannot vouch for, so only what is open to everyone (or belongs to this group's own
        // community) is offered.
        string? RestrictedReason(string? communityId, List<int>? yearGroups)
        {
            if (yearGroups is { Count: > 0 }) return "That item is only for some year groups, so it can't be posted to a group.";
            if (communityId is not null && communityId != group.CommunityId) return "That item belongs to a different community than this group.";
            return null;
        }

        string title, body, path;
        switch (request.Type?.ToLowerInvariant())
        {
            case "event":
                var ev = await eventRepo.GetByIdAsync(request.ContentId);
                if (ev is null) return new(null, null, null, null, "Event not found");
                if (RestrictedReason(ev.CommunityId, ev.YearGroups) is { } e1) return new(null, null, null, null, e1);
                title = ev.Title;
                body = $"{ev.StartDate:dddd d MMMM, h:mm tt}{(string.IsNullOrWhiteSpace(ev.Venue) ? "" : $" · {ev.Venue}")}";
                path = $"/events/{ev.Id}";
                break;
            case "job":
                var job = await jobRepo.GetByIdAsync(request.ContentId);
                if (job is null || job.Status != "Active") return new(null, null, null, null, "Opportunity not found or not live");
                if (RestrictedReason(job.CommunityId, job.YearGroups) is { } e2) return new(null, null, null, null, e2);
                title = job.Title;
                body = string.IsNullOrWhiteSpace(job.Location) ? job.Company : $"{job.Company} · {job.Location}";
                path = $"/jobs/{job.Id}";
                break;
            case "news":
                var post = await newsRepo.GetByIdAsync(request.ContentId);
                if (post is null || post.Status != "Published") return new(null, null, null, null, "Article not found or not published");
                if (RestrictedReason(post.CommunityId, post.YearGroups) is { } e3) return new(null, null, null, null, e3);
                title = post.Title;
                body = "Read the full story:";
                path = $"/news/{post.Id}";
                break;
            case "campaign":
                var campaign = await campaignRepo.GetByIdAsync(request.ContentId);
                if (campaign is null || campaign.Status != CampaignStatus.Active) return new(null, null, null, null, "Fundraiser not found or not active");
                if (RestrictedReason(campaign.CommunityId, campaign.YearGroups) is { } e4) return new(null, null, null, null, e4);
                title = campaign.Title;
                body = $"Open until {campaign.Deadline:d MMMM yyyy}. Every gift counts.";
                path = $"/payment-campaign/{campaign.Id}";
                break;
            default:
                return new(null, null, null, null, "Choose an event, an opportunity, a news article or a fundraiser.");
        }

        var link = $"{portal}{path}";
        var name = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
        return new(title, $"*{title}*\n{body}\n{link}\n— {name}", link, group, null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<(PostgresDb.Sdk.Entities.Institution? Institution, WhatsAppGroupLink? Link)> FindAsync(string groupId)
    {
        var institution = await LoadAsync();
        return (institution, institution?.WhatsAppGroups.FirstOrDefault(g => g.Id == groupId));
    }

    private async Task Save(PostgresDb.Sdk.Entities.Institution institution)
    {
        // Replaced wholesale so change tracking notices an edit made inside the list.
        institution.WhatsAppGroups = [.. institution.WhatsAppGroups];
        await institutionRepo.UpdateAsync(institution);
    }

    private async Task<Dictionary<string, string>> CommunityNames()
        => (await communityRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);

    private static WhatsAppGroupDto ToDto(WhatsAppGroupLink g, Dictionary<string, string> communities) => new(
        g.Id, g.Name, g.GroupJid, g.Status,
        g.Status == WhatsAppGroupStatuses.AwaitingGroup && g.CodeExpiresAt > DateTime.UtcNow ? g.Code : null,
        g.Status == WhatsAppGroupStatuses.AwaitingGroup ? g.CodeExpiresAt : null,
        g.CommunityId, g.CommunityId is not null && communities.TryGetValue(g.CommunityId, out var n) ? n : null,
        g.AssistantEnabled, g.AllowedCommands, g.PublishingEnabled, g.CreatedAt, g.LinkedAt, g.InviteLink);

    /// <summary>Only WhatsApp's own invite address: the link is shown to members, so it must never lead anywhere else.</summary>
    private static bool IsGroupInviteLink(string value)
        => value.Length <= 200 && !value.Any(char.IsWhiteSpace)
           && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
           && uri.Host.Equals("chat.whatsapp.com", StringComparison.OrdinalIgnoreCase)
           && uri.AbsolutePath.Trim('/').Length >= 10;

    private static string NewCode()
    {
        var sb = new StringBuilder(8);
        for (var i = 0; i < 8; i++) sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        return sb.ToString();
    }
}

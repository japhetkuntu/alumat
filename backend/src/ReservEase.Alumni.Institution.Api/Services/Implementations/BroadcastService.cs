using System.Linq.Expressions;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using CampaignEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Campaign;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using CampaignStatus = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.CampaignStatus;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class BroadcastService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<ContributionEntity> contributionRepo,
    IAlumniPgRepository<CampaignEntity> campaignRepo,
    IStorageService storageService,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    ILogger<BroadcastService> logger) : IBroadcastService
{
    /// <summary>How long since last login before a member counts as "Dormant" for EngagementSegments.Dormant.</summary>
    private const int DormantDaysThreshold = 60;

    private static Expression<Func<MemberEntity, bool>> BuildPredicate(BroadcastFilter filter, AuthData admin)
    {
        var isSuper = admin.Role != StaffRoles.ScopedAdmin;
        var yearGroups = admin.YearGroups ?? new List<int>();
        var dormantCutoff = DateTime.UtcNow.AddDays(-DormantDaysThreshold);
        var isDormantSegment = filter.EngagementSegment == EngagementSegments.Dormant;

        return m => (isSuper || yearGroups.Contains(m.GraduationYear))
            && (string.IsNullOrEmpty(filter.Status) || m.Status == filter.Status)
            && (string.IsNullOrEmpty(filter.DepartmentId) || m.DepartmentId == filter.DepartmentId)
            && (!filter.GraduationYearFrom.HasValue || m.GraduationYear >= filter.GraduationYearFrom.Value)
            && (!filter.GraduationYearTo.HasValue || m.GraduationYear <= filter.GraduationYearTo.Value)
            && (!isDormantSegment || m.LastLoginAt == null || m.LastLoginAt < dormantCutoff);
    }

    /// <summary>
    /// The two contribution-based segments need a cross-entity check (has this member ever paid /
    /// paid toward what's currently open) that a plain Member-field Expression can't express, so
    /// they're applied as an in-memory exclusion after the base predicate has already narrowed the
    /// set down to a filterable size — the same "materialize, then LINQ over it" pattern used
    /// throughout ScheduledJobsActivities for cross-entity work.
    /// </summary>
    private async Task<List<MemberEntity>> ApplyEngagementSegmentAsync(List<MemberEntity> members, string? segment)
    {
        if (members.Count == 0) return members;

        if (segment == EngagementSegments.NoContributionsEver)
        {
            var contributedIds = (await contributionRepo.GetAllAsync(c => c.Status == "Successful"))
                .Select(c => c.MemberId).ToHashSet();
            return members.Where(m => !contributedIds.Contains(m.Id)).ToList();
        }

        if (segment == EngagementSegments.NoContributionToActiveFundraiser)
        {
            var now = DateTime.UtcNow;
            var openFundraiserIds = (await campaignRepo.GetAllAsync(c =>
                    !c.IsMembershipCampaign && c.Status == CampaignStatus.Active && c.Deadline >= now))
                .Select(c => c.Id).ToHashSet();
            // No open fundraiser right now means the segment describes no one, not everyone —
            // sending "hasn't given to a fundraiser" when there is none to give to would be
            // meaningless (and confusing) rather than merely broad.
            if (openFundraiserIds.Count == 0) return [];

            var contributedIds = (await contributionRepo.GetAllAsync(c =>
                    c.Status == "Successful" && openFundraiserIds.Contains(c.CampaignId)))
                .Select(c => c.MemberId).ToHashSet();
            return members.Where(m => !contributedIds.Contains(m.Id)).ToList();
        }

        return members;
    }

    private async Task<List<MemberEntity>> ResolveRecipientsAsync(BroadcastFilter filter, AuthData admin)
    {
        var members = await memberRepo.GetAllAsync(BuildPredicate(filter, admin));
        return await ApplyEngagementSegmentAsync(members.ToList(), filter.EngagementSegment);
    }

    public async Task<IApiResponse<int>> GetRecipientCountAsync(BroadcastFilter filter, AuthData admin)
    {
        try
        {
            var members = await ResolveRecipientsAsync(filter, admin);
            return members.Count.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error counting broadcast recipients with filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<int>("Failed to count recipients");
        }
    }

    public async Task<IApiResponse<BroadcastResult>> SendBroadcastAsync(SendBroadcastRequest request, AuthData admin)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponseExtensions.ToBadRequestApiResponse<BroadcastResult>("Message is required");

            var channels = request.Channels.Count == 0 ? new List<string> { "InApp" } : request.Channels;

            var members = await ResolveRecipientsAsync(request.ToFilter(), admin);
            var recipients = members
                .Select(m => new BroadcastRecipient(m.Id, m.Email, m.FirstName, m.Phone))
                .ToList();

            string? imageUrl = null;
            if (request.Image is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.Image.FileName)}";
                imageUrl = await storageService.UploadFileAsync(request.Image, name, institutionSlug: currentTenant.InstitutionSlug ?? "");
            }

            if (recipients.Count > 0)
            {
                await temporalProvider.EnqueueNotificationAsync(
                    NotificationRequest.Broadcast(currentTenant.InstitutionId!, recipients, request.Title, request.Message, channels, imageUrl), logger);
            }

            logger.LogInformation("Broadcast queued by admin {AdminId} to {Count} recipients via [{Channels}] (segment: {Segment})",
                admin.Id, recipients.Count, string.Join(",", channels), request.EngagementSegment ?? "none");

            return new BroadcastResult(recipients.Count, channels).ToOkApiResponse("Broadcast queued");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending broadcast by admin {AdminId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BroadcastResult>("Failed to send broadcast");
        }
    }
}

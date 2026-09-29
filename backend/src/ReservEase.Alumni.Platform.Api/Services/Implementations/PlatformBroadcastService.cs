using System.Linq.Expressions;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using CampaignEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Campaign;
using CampaignStatus = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.CampaignStatus;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Lets platform staff send the same kind of targeted, image-capable notification to one
/// institution's members that BroadcastService already lets that institution's own admins
/// send to themselves — for support/onboarding outreach (e.g. Support nudging inactive
/// members on an institution's behalf). Same engagement-segment logic, duplicated rather
/// than shared (the two APIs don't share a services layer), but every query here goes
/// ignoreQueryFilters: true with an explicit InstitutionId, the same way PlatformMemberService
/// and every other cross-institution read in this API does — platform staff have no single
/// tenant of their own to rely on the automatic tenant filter for.
/// </summary>
public class PlatformBroadcastService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<ContributionEntity> contributionRepo,
    IAlumniPgRepository<CampaignEntity> campaignRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    ITemporalClientProvider temporalProvider,
    IAuditLogService auditLog,
    ILogger<PlatformBroadcastService> logger) : IPlatformBroadcastService
{
    private const int DormantDaysThreshold = 60;

    private static Expression<Func<MemberEntity, bool>> BuildPredicate(PlatformBroadcastFilter filter)
    {
        var dormantCutoff = DateTime.UtcNow.AddDays(-DormantDaysThreshold);
        var isDormantSegment = filter.EngagementSegment == PlatformEngagementSegments.Dormant;

        return m => m.InstitutionId == filter.InstitutionId
            && (string.IsNullOrEmpty(filter.Status) || m.Status == filter.Status)
            && (string.IsNullOrEmpty(filter.DepartmentId) || m.DepartmentId == filter.DepartmentId)
            && (!filter.GraduationYearFrom.HasValue || m.GraduationYear >= filter.GraduationYearFrom.Value)
            && (!filter.GraduationYearTo.HasValue || m.GraduationYear <= filter.GraduationYearTo.Value)
            && (!isDormantSegment || m.LastLoginAt == null || m.LastLoginAt < dormantCutoff);
    }

    /// <summary>See BroadcastService.ApplyEngagementSegmentAsync (Institution.Api) — identical
    /// logic, just scoped by an explicit institutionId instead of the current tenant.</summary>
    private async Task<List<MemberEntity>> ApplyEngagementSegmentAsync(List<MemberEntity> members, string institutionId, string? segment)
    {
        if (members.Count == 0) return members;

        if (segment == PlatformEngagementSegments.NoContributionsEver)
        {
            var contributedIds = (await contributionRepo.GetAllAsync(
                    c => c.InstitutionId == institutionId && c.Status == "Successful", ignoreQueryFilters: true))
                .Select(c => c.MemberId).ToHashSet();
            return members.Where(m => !contributedIds.Contains(m.Id)).ToList();
        }

        if (segment == PlatformEngagementSegments.NoContributionToActiveFundraiser)
        {
            var now = DateTime.UtcNow;
            var openFundraiserIds = (await campaignRepo.GetAllAsync(c =>
                    c.InstitutionId == institutionId && !c.IsMembershipCampaign
                    && c.Status == CampaignStatus.Active && c.Deadline >= now, ignoreQueryFilters: true))
                .Select(c => c.Id).ToHashSet();
            if (openFundraiserIds.Count == 0) return [];

            var contributedIds = (await contributionRepo.GetAllAsync(c =>
                    c.InstitutionId == institutionId && c.Status == "Successful" && openFundraiserIds.Contains(c.CampaignId), ignoreQueryFilters: true))
                .Select(c => c.MemberId).ToHashSet();
            return members.Where(m => !contributedIds.Contains(m.Id)).ToList();
        }

        return members;
    }

    private async Task<List<MemberEntity>> ResolveRecipientsAsync(PlatformBroadcastFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.InstitutionId)) return [];
        var members = await memberRepo.GetAllAsync(BuildPredicate(filter), ignoreQueryFilters: true);
        return await ApplyEngagementSegmentAsync(members.ToList(), filter.InstitutionId, filter.EngagementSegment);
    }

    public async Task<IApiResponse<int>> GetRecipientCountAsync(PlatformBroadcastFilter filter)
    {
        try
        {
            var members = await ResolveRecipientsAsync(filter);
            return members.Count.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error counting platform broadcast recipients (institutionId={InstitutionId})", filter.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<int>("Failed to count recipients");
        }
    }

    public async Task<IApiResponse<PlatformBroadcastResult>> SendBroadcastAsync(SendPlatformBroadcastRequest request, AuthData admin)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.InstitutionId))
                return ApiResponseExtensions.ToBadRequestApiResponse<PlatformBroadcastResult>("InstitutionId is required");
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponseExtensions.ToBadRequestApiResponse<PlatformBroadcastResult>("Message is required");

            var institution = await institutionRepo.GetOneAsync(i => i.Id == request.InstitutionId, ignoreQueryFilters: true);
            if (institution is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<PlatformBroadcastResult>("Institution not found");

            var channels = request.Channels.Count == 0 ? new List<string> { "InApp" } : request.Channels;
            var members = await ResolveRecipientsAsync(request);
            var recipients = members
                .Select(m => new BroadcastRecipient(m.Id, m.Email, m.FirstName, m.Phone))
                .ToList();

            if (recipients.Count > 0)
            {
                await temporalProvider.EnqueueNotificationAsync(
                    NotificationRequest.Broadcast(request.InstitutionId, recipients, request.Title, request.Message, channels, request.ImageUrl), logger);
            }

            await auditLog.LogAsync(admin.Id, $"{admin.FirstName} {admin.LastName}".Trim(), "sent member broadcast",
                $"{institution.Name} — {recipients.Count} recipient(s), via {string.Join("+", channels)} (segment: {request.EngagementSegment ?? "none"})");

            logger.LogInformation("Platform broadcast queued by staff {StaffId} to {Count} members of institution {InstitutionId} via [{Channels}]",
                admin.Id, recipients.Count, request.InstitutionId, string.Join(",", channels));

            return new PlatformBroadcastResult(recipients.Count, channels).ToOkApiResponse("Broadcast queued");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending platform broadcast by staff {StaffId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PlatformBroadcastResult>("Failed to send broadcast");
        }
    }
}

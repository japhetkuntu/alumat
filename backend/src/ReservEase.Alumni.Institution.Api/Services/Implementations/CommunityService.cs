using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class CommunityService(
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Referral> referralRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    ICurrentTenantService currentTenant,
    IRedisService<PublicContentCacheConfig> publicCache,
    IInstitutionAuditLogService auditLog,
    ILogger<CommunityService> logger) : ICommunityService
{
    /// <summary>The landing page lists the groups marked public, with a head count, so anything that changes a group or its membership clears that list.</summary>
    private Task InvalidatePublicGroupsCacheAsync()
        => currentTenant.InstitutionId is { } id ? publicCache.RemoveAsync(PublicContentCacheKeys.Communities(id)) : Task.CompletedTask;

    public async Task<IApiResponse<List<CommunityListItem>>> GetCommunitiesAsync()
    {
        try
        {
            var communities = (await communityRepo.GetAllAsync(_ => true)).ToList();
            var communityIds = communities.Select(c => c.Id).ToList();

            // One fetch of every membership row across every community instead
            // of a separate query (pulling full rows) per community.
            var membershipsByCommunity = (await membershipRepo.GetAllAsync(m => communityIds.Contains(m.CommunityId)))
                .ToLookup(m => m.CommunityId);

            var since = DateTime.UtcNow.AddDays(-30);
            var invited = (await referralRepo.GetAllAsync(r => r.CommunityId != null && communityIds.Contains(r.CommunityId))).ToLookup(r => r.CommunityId!);

            var items = communities.OrderByDescending(c => c.CreatedAt).Select(c =>
            {
                var memberships = membershipsByCommunity[c.Id];
                return new CommunityListItem(
                    c.Id, c.Name, c.Description, c.CoverImageUrl, c.IsActive,
                    memberships.Count(m => m.Status == "Approved" && m.Role == "Member"),
                    memberships.Count(m => m.Status == "Pending"),
                    memberships.Count(m => m.Status == "Approved" && m.Role == "Leader"),
                    c.CreatedAt, invited[c.Id].Count(), invited[c.Id].Count(r => r.CreatedAt >= since), c.Kind, c.IsPublic);
            }).ToList();
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving communities");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityListItem>>("Failed to retrieve communities");
        }
    }

    public async Task<IApiResponse<CommunityGrowthSummary>> GetGrowthAsync()
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-30);
            var invited = (await referralRepo.GetAllAsync(r => r.CommunityId != null)).ToList();
            var names = (await communityRepo.GetAllAsync(_ => true)).ToDictionary(c => c.Id, c => c.Name);

            var bySource = invited.GroupBy(r => r.Channel ?? "link").Select(g => new GrowthSourceItem(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList();
            var byCommunity = invited.GroupBy(r => r.CommunityId!)
                .Select(g => new GrowthCommunityItem(g.Key, names.GetValueOrDefault(g.Key, "Removed community"), g.Count(), g.Count(r => r.CreatedAt >= since)))
                .OrderByDescending(x => x.Joined).ToList();
            return new CommunityGrowthSummary(invited.Count, invited.Count(r => r.CreatedAt >= since), bySource, byCommunity).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building community growth summary");
            return ApiResponseExtensions.ToServerErrorApiResponse<CommunityGrowthSummary>("Failed to load growth");
        }
    }

    public async Task<IApiResponse<CommunityListItem>> CreateCommunityAsync(CreateCommunityRequest request, string createdBy)
    {
        try
        {
            var descriptionHtml = RichText.Sanitize(request.Description);
            if (RichText.IsTooLong(descriptionHtml))
                return ApiResponseExtensions.ToBadRequestApiResponse<CommunityListItem>(RichText.TooLongMessage);
            var community = new Community
            {
                Name = request.Name.Trim(),
                Description = descriptionHtml,
                CoverImageUrl = request.CoverImageUrl,
                Kind = string.IsNullOrWhiteSpace(request.Kind) ? null : request.Kind.Trim(),
                IsPublic = request.IsPublic,
                CreatedBy = createdBy,
            };
            await communityRepo.AddAsync(community);
            await InvalidatePublicGroupsCacheAsync();

            logger.LogInformation("Community {CommunityId} created by {CreatorId}", community.Id, createdBy);
            return new CommunityListItem(community.Id, community.Name, community.Description, community.CoverImageUrl, community.IsActive, 0, 0, 0, community.CreatedAt, Kind: community.Kind, IsPublic: community.IsPublic)
                .ToCreatedApiResponse("Community created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating community");
            return ApiResponseExtensions.ToServerErrorApiResponse<CommunityListItem>("Failed to create community");
        }
    }

    public async Task<IApiResponse<CommunityListItem>> UpdateCommunityAsync(string id, UpdateCommunityRequest request, string updatedBy)
    {
        try
        {
            var community = await communityRepo.GetByIdAsync(id);
            if (community is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CommunityListItem>("Community not found");

            community.Name = request.Name.Trim();
            var descriptionHtml = RichText.Sanitize(request.Description);
            if (RichText.IsTooLong(descriptionHtml))
                return ApiResponseExtensions.ToBadRequestApiResponse<CommunityListItem>(RichText.TooLongMessage);
            community.Description = descriptionHtml;
            community.CoverImageUrl = request.CoverImageUrl;
            // Null leaves the kind as it is (so toggling a group on or off never wipes it); a blank clears it.
            if (request.Kind is not null) community.Kind = string.IsNullOrWhiteSpace(request.Kind) ? null : request.Kind.Trim();
            community.IsActive = request.IsActive;
            if (request.IsPublic is not null) community.IsPublic = request.IsPublic.Value;
            community.UpdatedAt = DateTime.UtcNow;
            community.UpdatedBy = updatedBy;
            await communityRepo.UpdateAsync(community);
            await InvalidatePublicGroupsCacheAsync();

            var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == id)).ToList();
            return new CommunityListItem(
                community.Id, community.Name, community.Description, community.CoverImageUrl, community.IsActive,
                memberships.Count(m => m.Status == "Approved" && m.Role == "Member"),
                memberships.Count(m => m.Status == "Pending"),
                memberships.Count(m => m.Status == "Approved" && m.Role == "Leader"),
                community.CreatedAt, Kind: community.Kind, IsPublic: community.IsPublic).ToOkApiResponse("Community updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating community {CommunityId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<CommunityListItem>("Failed to update community");
        }
    }

    public async Task<IApiResponse<List<CommunityMemberItem>>> GetCommunityMembersAsync(string communityId)
    {
        try
        {
            var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == communityId)).ToList();
            var memberIds = memberships.Select(m => m.MemberId).ToHashSet();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);

            var items = memberships
                .OrderByDescending(m => m.RequestedAt)
                .Select(m => members.TryGetValue(m.MemberId, out var mem)
                    ? new CommunityMemberItem(m.Id, m.MemberId, $"{mem.FirstName} {mem.LastName}", mem.Email, m.Role, m.Status, m.RequestedAt)
                    : new CommunityMemberItem(m.Id, m.MemberId, "(deleted member)", "", m.Role, m.Status, m.RequestedAt))
                .ToList();

            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving members for community {CommunityId}", communityId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityMemberItem>>("Failed to retrieve community members");
        }
    }

    /// <summary>Tells a member what happened to their place in a group. Quiet if the member no longer exists.</summary>
    private async Task TellMemberAsync(string memberId, string title, string body, string communityId, string actionUrl, string admin)
    {
        try
        {
            if (await memberRepo.GetByIdAsync(memberId) is null) return;
            await notificationRepo.AddAsync(new Notification
            {
                RecipientId = memberId, RecipientType = "Member", Title = title, Body = body, Type = "CommunityMembership",
                RelatedEntityId = communityId, RelatedEntityType = "Community", ActionUrl = actionUrl, CreatedBy = admin,
            });
        }
        catch (Exception e)
        {
            // The decision is already saved; a missed notification must not undo it.
            logger.LogWarning(e, "Could not notify member {MemberId} about their place in community {CommunityId}", memberId, communityId);
        }
    }

    private static string? CleanReason(string? reason)
    {
        var r = reason?.Trim();
        return string.IsNullOrEmpty(r) ? null : (r.Length > 300 ? r[..300] : r);
    }

    public async Task<IApiResponse<object>> DecideJoinRequestAsync(string communityId, string memberId, bool approve, string? reason, AuthData admin)
    {
        try
        {
            var community = await communityRepo.GetByIdAsync(communityId);
            var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
            if (community is null || membership is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("That request was not found");
            // Someone else (a leader, or another administrator) may have decided it a moment ago. Say so rather than deciding twice.
            // A request that was declined can still be approved later (the administrator changed their mind); nothing else can be decided again.
            var decidable = membership.Status == "Pending" || (approve && membership.Status == "Rejected");
            if (!decidable)
                return ApiResponseExtensions.ToConflictApiResponse<object>(membership.Status == "Approved"
                    ? "This request has already been approved."
                    : "This request has already been declined.");

            var note = CleanReason(reason);
            membership.Status = approve ? "Approved" : "Rejected";
            membership.DecidedAt = DateTime.UtcNow;
            membership.DecidedBy = admin.Id;
            membership.UpdatedAt = DateTime.UtcNow;
            membership.UpdatedBy = admin.Id;
            await membershipRepo.UpdateAsync(membership);
            await InvalidatePublicGroupsCacheAsync();

            if (approve)
                await TellMemberAsync(memberId, "You're in", $"Your request to join \"{community.Name}\" was approved.", communityId, $"/communities/{communityId}", admin.Id);
            else
                await TellMemberAsync(memberId, "About your request",
                    note is null ? $"Your request to join \"{community.Name}\" was not approved this time." : $"Your request to join \"{community.Name}\" was not approved this time. {note}",
                    communityId, "/communities", admin.Id);
            await auditLog.LogAsync(admin, approve ? "Group Request Approved" : "Group Request Declined", community.Name);
            return new object().ToOkApiResponse(approve ? "Request approved" : "Request declined");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deciding join request for community {CommunityId} member {MemberId}", communityId, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to save your decision");
        }
    }

    public async Task<IApiResponse<object>> RemoveMemberAsync(string communityId, string memberId, string? reason, AuthData admin)
    {
        try
        {
            var community = await communityRepo.GetByIdAsync(communityId);
            var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
            if (community is null || membership is null || membership.Status != "Approved")
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("That member is not in this group");

            var note = CleanReason(reason);
            await membershipRepo.RemoveAsync(membership);
            await InvalidatePublicGroupsCacheAsync();

            await TellMemberAsync(memberId, "You were removed from a group",
                note is null ? $"You were removed from \"{community.Name}\". You can ask to join again." : $"You were removed from \"{community.Name}\". {note}",
                communityId, "/communities", admin.Id);
            await auditLog.LogAsync(admin, "Group Member Removed", community.Name);
            return new object().ToOkApiResponse("Member removed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error removing member {MemberId} from community {CommunityId}", memberId, communityId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to remove the member");
        }
    }

    public async Task<IApiResponse<object>> SetMemberRoleAsync(string communityId, string memberId, SetCommunityMemberRoleRequest request, string updatedBy)
    {
        try
        {
            if (request.Role != "Member" && request.Role != "Leader")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Role must be Member or Leader");

            var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
            if (membership is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Membership not found — the member must request to join first");

            // Admin assigning a role is itself an approval (this is also how the
            // very first Leader gets set — otherwise no one could ever approve
            // the first join request, since only a Leader can do that normally).
            if (membership.Status != "Approved")
            {
                membership.Status = "Approved";
                membership.DecidedAt = DateTime.UtcNow;
                membership.DecidedBy = updatedBy;
            }
            membership.Role = request.Role;
            membership.UpdatedAt = DateTime.UtcNow;
            membership.UpdatedBy = updatedBy;
            await membershipRepo.UpdateAsync(membership);
            await InvalidatePublicGroupsCacheAsync();

            logger.LogInformation("Community {CommunityId} member {MemberId} role set to {Role} by {UpdaterId}", communityId, memberId, request.Role, updatedBy);
            return new object().ToOkApiResponse($"Role updated to {request.Role}");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error setting role for community {CommunityId} member {MemberId}", communityId, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to update role");
        }
    }
}

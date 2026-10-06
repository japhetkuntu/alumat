using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class CommunityService(
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Referral> referralRepo,
    ILogger<CommunityService> logger) : ICommunityService
{
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
                    c.CreatedAt, ToChannelItems(c.ExternalChannels), invited[c.Id].Count(), invited[c.Id].Count(r => r.CreatedAt >= since));
            }).ToList();
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving communities");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityListItem>>("Failed to retrieve communities");
        }
    }

    private static List<CommunityChannelItem>? ToChannelItems(List<CommunityChannel>? channels) =>
        channels is { Count: > 0 } ? channels.Select(c => new CommunityChannelItem(c.Type, c.DisplayName, c.InviteUrl, c.ConnectedAt)).ToList() : null;

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
            var community = new Community
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                CoverImageUrl = request.CoverImageUrl,
                CreatedBy = createdBy,
            };
            await communityRepo.AddAsync(community);

            logger.LogInformation("Community {CommunityId} created by {CreatorId}", community.Id, createdBy);
            return new CommunityListItem(community.Id, community.Name, community.Description, community.CoverImageUrl, community.IsActive, 0, 0, 0, community.CreatedAt)
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
            community.Description = request.Description;
            community.CoverImageUrl = request.CoverImageUrl;
            community.IsActive = request.IsActive;
            if (request.ExternalChannels is not null)
            {
                var parsed = CommunityChannelRules.Parse(request.ExternalChannels.Select(c => (c.Type, c.DisplayName, c.InviteUrl)), community.ExternalChannels, updatedBy, out var channelError);
                if (parsed is null)
                    return ApiResponseExtensions.ToBadRequestApiResponse<CommunityListItem>(channelError!);
                community.ExternalChannels = parsed.Count == 0 ? null : parsed;
            }
            community.UpdatedAt = DateTime.UtcNow;
            community.UpdatedBy = updatedBy;
            await communityRepo.UpdateAsync(community);

            var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == id)).ToList();
            return new CommunityListItem(
                community.Id, community.Name, community.Description, community.CoverImageUrl, community.IsActive,
                memberships.Count(m => m.Status == "Approved" && m.Role == "Member"),
                memberships.Count(m => m.Status == "Pending"),
                memberships.Count(m => m.Status == "Approved" && m.Role == "Leader"),
                community.CreatedAt, ToChannelItems(community.ExternalChannels)).ToOkApiResponse("Community updated");
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

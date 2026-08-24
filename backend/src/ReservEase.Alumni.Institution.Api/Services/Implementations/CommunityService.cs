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
    ILogger<CommunityService> logger) : ICommunityService
{
    public async Task<IApiResponse<List<CommunityListItem>>> GetCommunitiesAsync()
    {
        try
        {
            var communities = await communityRepo.GetAllAsync(_ => true);
            var items = new List<CommunityListItem>();
            foreach (var c in communities.OrderByDescending(c => c.CreatedAt))
            {
                var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == c.Id)).ToList();
                items.Add(new CommunityListItem(
                    c.Id, c.Name, c.Description, c.CoverImageUrl, c.IsActive,
                    memberships.Count(m => m.Status == "Approved" && m.Role == "Member"),
                    memberships.Count(m => m.Status == "Pending"),
                    memberships.Count(m => m.Status == "Approved" && m.Role == "Leader"),
                    c.CreatedAt));
            }
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving communities");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityListItem>>("Failed to retrieve communities");
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
            community.UpdatedAt = DateTime.UtcNow;
            community.UpdatedBy = updatedBy;
            await communityRepo.UpdateAsync(community);

            var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == id)).ToList();
            return new CommunityListItem(
                community.Id, community.Name, community.Description, community.CoverImageUrl, community.IsActive,
                memberships.Count(m => m.Status == "Approved" && m.Role == "Member"),
                memberships.Count(m => m.Status == "Pending"),
                memberships.Count(m => m.Status == "Approved" && m.Role == "Leader"),
                community.CreatedAt).ToOkApiResponse("Community updated");
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

using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class CommunityService(
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ILogger<CommunityService> logger) : ICommunityService
{
    private async Task<CommunityMembership?> GetLeaderMembershipAsync(string communityId, string memberId)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
        return membership is { Status: "Approved", Role: "Leader" } ? membership : null;
    }

    public async Task<IApiResponse<List<CommunityDto>>> GetCommunitiesAsync(string memberId)
    {
        try
        {
            var communities = (await communityRepo.GetAllAsync(c => c.IsActive)).ToList();
            var myMemberships = (await membershipRepo.GetAllAsync(m => m.MemberId == memberId))
                .ToDictionary(m => m.CommunityId);

            var items = new List<CommunityDto>();
            foreach (var c in communities.OrderBy(c => c.Name))
            {
                var approvedCount = await membershipRepo.CountAsync(m => m.CommunityId == c.Id && m.Status == "Approved");
                myMemberships.TryGetValue(c.Id, out var mine);
                items.Add(new CommunityDto(c.Id, c.Name, c.Description, c.CoverImageUrl, approvedCount, mine?.Status, mine?.Status == "Approved" ? mine.Role : null));
            }
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving communities for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityDto>>("Failed to retrieve communities");
        }
    }

    public async Task<IApiResponse<List<CommunityDto>>> GetMyCommunitiesAsync(string memberId)
    {
        try
        {
            var myMemberships = (await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved")).ToList();
            var communityIds = myMemberships.Select(m => m.CommunityId).ToHashSet();
            var communities = (await communityRepo.GetAllAsync(c => communityIds.Contains(c.Id) && c.IsActive))
                .ToDictionary(c => c.Id);

            var items = new List<CommunityDto>();
            foreach (var m in myMemberships)
            {
                if (!communities.TryGetValue(m.CommunityId, out var c)) continue;
                var approvedCount = await membershipRepo.CountAsync(mm => mm.CommunityId == c.Id && mm.Status == "Approved");
                items.Add(new CommunityDto(c.Id, c.Name, c.Description, c.CoverImageUrl, approvedCount, m.Status, m.Role));
            }
            return items.OrderBy(i => i.Name).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving my communities for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityDto>>("Failed to retrieve your communities");
        }
    }

    public async Task<IApiResponse<CommunityDto>> GetCommunityAsync(string id, string memberId)
    {
        try
        {
            var community = await communityRepo.GetByIdAsync(id);
            if (community is null || !community.IsActive)
                return ApiResponseExtensions.ToNotFoundApiResponse<CommunityDto>("Community not found");

            var mine = await membershipRepo.GetOneAsync(m => m.CommunityId == id && m.MemberId == memberId);
            var approvedCount = await membershipRepo.CountAsync(m => m.CommunityId == id && m.Status == "Approved");
            return new CommunityDto(community.Id, community.Name, community.Description, community.CoverImageUrl, approvedCount, mine?.Status, mine?.Status == "Approved" ? mine.Role : null)
                .ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving community {CommunityId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<CommunityDto>("Failed to retrieve community");
        }
    }

    public async Task<IApiResponse<object>> JoinAsync(string id, string memberId)
    {
        try
        {
            var community = await communityRepo.GetByIdAsync(id);
            if (community is null || !community.IsActive)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Community not found");

            var existing = await membershipRepo.GetOneAsync(m => m.CommunityId == id && m.MemberId == memberId);
            if (existing is not null)
            {
                if (existing.Status is "Pending" or "Approved")
                    return ApiResponseExtensions.ToConflictApiResponse<object>($"You already have a {existing.Status.ToLower()} request for this community");

                // Re-requesting after a rejection — reset to Pending.
                existing.Status = "Pending";
                existing.RequestedAt = DateTime.UtcNow;
                existing.DecidedAt = null;
                existing.DecidedBy = null;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = memberId;
                await membershipRepo.UpdateAsync(existing);
                return new object().ToOkApiResponse("Join request submitted");
            }

            var membership = new CommunityMembership
            {
                CommunityId = id,
                MemberId = memberId,
                CreatedBy = memberId,
            };
            await membershipRepo.AddAsync(membership);

            logger.LogInformation("Member {MemberId} requested to join community {CommunityId}", memberId, id);
            return new object().ToCreatedApiResponse("Join request submitted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error joining community {CommunityId} for member {MemberId}", id, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to submit join request");
        }
    }

    public async Task<IApiResponse<object>> LeaveAsync(string id, string memberId)
    {
        try
        {
            var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == id && m.MemberId == memberId);
            if (membership is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("You are not a member of this community");

            await membershipRepo.RemoveAsync(membership);
            logger.LogInformation("Member {MemberId} left community {CommunityId}", memberId, id);
            return new object().ToOkApiResponse("Left community");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error leaving community {CommunityId} for member {MemberId}", id, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to leave community");
        }
    }

    public async Task<IApiResponse<List<CommunityMemberDto>>> GetMembersAsync(string id, string memberId)
    {
        try
        {
            var mine = await membershipRepo.GetOneAsync(m => m.CommunityId == id && m.MemberId == memberId);
            if (mine is not { Status: "Approved" })
                return ApiResponseExtensions.ToForbiddenApiResponse<List<CommunityMemberDto>>("You must be an approved member to view this community's members");

            var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == id && m.Status == "Approved")).ToList();
            var memberIds = memberships.Select(m => m.MemberId).ToHashSet();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);

            var items = memberships
                .Where(m => members.ContainsKey(m.MemberId))
                .Select(m =>
                {
                    var mem = members[m.MemberId];
                    return new CommunityMemberDto(mem.Id, $"{mem.FirstName} {mem.LastName}", mem.ProfilePictureUrl, m.Role);
                })
                .OrderBy(d => d.Name)
                .ToList();

            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving members for community {CommunityId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<CommunityMemberDto>>("Failed to retrieve members");
        }
    }

    public async Task<IApiResponse<List<JoinRequestDto>>> GetJoinRequestsAsync(string id, string memberId)
    {
        try
        {
            if (await GetLeaderMembershipAsync(id, memberId) is null)
                return ApiResponseExtensions.ToForbiddenApiResponse<List<JoinRequestDto>>("Only a community leader can view join requests");

            var pending = (await membershipRepo.GetAllAsync(m => m.CommunityId == id && m.Status == "Pending")).ToList();
            var memberIds = pending.Select(m => m.MemberId).ToHashSet();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);

            var items = pending
                .Where(m => members.ContainsKey(m.MemberId))
                .OrderBy(m => m.RequestedAt)
                .Select(m =>
                {
                    var mem = members[m.MemberId];
                    return new JoinRequestDto(m.Id, mem.Id, $"{mem.FirstName} {mem.LastName}", mem.ProfilePictureUrl, m.RequestedAt);
                })
                .ToList();

            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving join requests for community {CommunityId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<JoinRequestDto>>("Failed to retrieve join requests");
        }
    }

    public async Task<IApiResponse<object>> DecideJoinRequestAsync(string id, string membershipId, bool approve, string actingMemberId)
    {
        try
        {
            if (await GetLeaderMembershipAsync(id, actingMemberId) is null)
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only a community leader can decide join requests");

            var membership = await membershipRepo.GetByIdAsync(membershipId);
            if (membership is null || membership.CommunityId != id || membership.Status != "Pending")
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Pending join request not found");

            membership.Status = approve ? "Approved" : "Rejected";
            membership.DecidedAt = DateTime.UtcNow;
            membership.DecidedBy = actingMemberId;
            membership.UpdatedAt = DateTime.UtcNow;
            membership.UpdatedBy = actingMemberId;
            await membershipRepo.UpdateAsync(membership);

            logger.LogInformation("Community {CommunityId} join request {MembershipId} {Decision} by leader {LeaderId}", id, membershipId, membership.Status, actingMemberId);
            return new object().ToOkApiResponse($"Request {membership.Status.ToLower()}");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deciding join request {MembershipId} for community {CommunityId}", membershipId, id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to decide join request");
        }
    }

    public async Task<IApiResponse<object>> RemoveMemberAsync(string id, string targetMemberId, string actingMemberId)
    {
        try
        {
            if (await GetLeaderMembershipAsync(id, actingMemberId) is null)
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only a community leader can remove members");

            var target = await membershipRepo.GetOneAsync(m => m.CommunityId == id && m.MemberId == targetMemberId);
            if (target is null || target.Status != "Approved")
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found in this community");

            if (target.Role == "Leader")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("A leader can't be removed this way — ask an institution admin to change their role first");

            await membershipRepo.RemoveAsync(target);
            logger.LogInformation("Community {CommunityId} member {MemberId} removed by leader {LeaderId}", id, targetMemberId, actingMemberId);
            return new object().ToOkApiResponse("Member removed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error removing member {MemberId} from community {CommunityId}", targetMemberId, id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to remove member");
        }
    }
}

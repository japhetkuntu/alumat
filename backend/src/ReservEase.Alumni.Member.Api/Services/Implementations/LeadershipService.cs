using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class LeadershipService(
    IAlumniPgRepository<LeadershipPost> postRepo,
    IAlumniPgRepository<LeadershipRole> roleRepo,
    LeadershipAccess access,
    IAlumniPgRepository<PostgresDb.Sdk.Entities.Alumni.Member> memberRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<InstitutionAuditLogEntry> auditRepo,
    ILogger<LeadershipService> logger) : ILeadershipService
{
    private const int MaxMemberIds = 100;

    public async Task<IApiResponse<List<LeadershipPostDto>>> GetPostsAsync(string? scopeType, string? scopeId, IReadOnlyCollection<string>? memberIds)
    {
        try
        {
            var ids = memberIds?.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Take(MaxMemberIds).ToList();
            var byMembers = ids is { Count: > 0 };
            // Asking for nothing in particular would return every post in the community; a page always names a group or a set of people.
            if (!byMembers && (string.IsNullOrEmpty(scopeType) || !LeadershipPost.IsValidScope(scopeType)))
                return new List<LeadershipPostDto>().ToOkApiResponse();

            var posts = (await postRepo.GetAllAsync(p =>
                (byMembers ? ids!.Contains(p.MemberId) : p.ScopeType == scopeType && p.ScopeId == (scopeId ?? string.Empty)))).OrderBy(p => p.CreatedAt).ToList();
            if (posts.Count == 0) return new List<LeadershipPostDto>().ToOkApiResponse();

            var roleKeys = posts.Select(p => p.RoleId).Distinct().ToList();
            var roleNames = (await roleRepo.GetAllAsync(r => roleKeys.Contains(r.Id))).ToDictionary(r => r.Id, r => r.Name);
            var memberKeys = posts.Select(p => p.MemberId).Distinct().ToList();
            var members = (await memberRepo.GetAllAsync(m => memberKeys.Contains(m.Id) && m.Status == "Active")).ToDictionary(m => m.Id);
            var batches = (await batchRepo.GetAllAsync(_ => true)).ToDictionary(b => b.Id, b => b.Name);
            var communities = (await communityRepo.GetAllAsync(_ => true)).ToDictionary(c => c.Id, c => c.Name);

            return posts.Where(p => members.ContainsKey(p.MemberId) && roleNames.ContainsKey(p.RoleId)).Select(p =>
            {
                var m = members[p.MemberId];
                var scopeName = p.ScopeType switch
                {
                    LeadershipPost.ScopeBatch => batches.GetValueOrDefault(p.ScopeId),
                    LeadershipPost.ScopeCommunity => communities.GetValueOrDefault(p.ScopeId),
                    _ => string.Empty,
                } ?? string.Empty;
                return new LeadershipPostDto(p.Id, p.MemberId, $"{m.FirstName} {m.LastName}".Trim(), m.ProfilePictureUrl, m.JobTitle,
                    roleNames[p.RoleId], p.ScopeType, p.ScopeId, scopeName);
            }).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving leadership (scope={ScopeType}/{ScopeId})", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<LeadershipPostDto>>("Failed to retrieve leadership");
        }
    }

    public async Task<IApiResponse<List<MyRoleDto>>> GetMineAsync(string memberId)
    {
        try
        {
            var held = await access.GetHeldAsync(memberId);
            if (held.Count == 0) return new List<MyRoleDto>().ToOkApiResponse();
            var batches = (await batchRepo.GetAllAsync(_ => true)).ToDictionary(b => b.Id, b => b.Name);
            var communities = (await communityRepo.GetAllAsync(_ => true)).ToDictionary(c => c.Id, c => c.Name);
            return held.Select(h => new MyRoleDto(h.PostId, h.RoleName, h.ScopeType, h.ScopeId,
                h.ScopeType switch
                {
                    LeadershipPost.ScopeBatch => batches.GetValueOrDefault(h.ScopeId),
                    LeadershipPost.ScopeCommunity => communities.GetValueOrDefault(h.ScopeId),
                    _ => string.Empty,
                } ?? string.Empty,
                LeadershipPrivileges.All.Where(p => h.Privileges.Contains(p.Key)).Select(p => new PrivilegeDto(p.Key, p.Label, p.Description)).ToList())).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving leadership roles for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<MyRoleDto>>("Failed to retrieve your roles");
        }
    }

    /// <summary>
    /// A leader giving up a role of their own. The institution did the appointing, so this only removes the post; the member stays a member.
    /// It is recorded in the audit log so the administrators can see it and appoint someone else.
    /// </summary>
    public async Task<IApiResponse<object>> StepDownAsync(string memberId, string postId)
    {
        try
        {
            var post = await postRepo.GetByIdAsync(postId);
            // Someone else's post is reported as not found, so nobody can tell which posts exist.
            if (post is null || post.MemberId != memberId) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That role was not found");

            var role = await roleRepo.GetByIdAsync(post.RoleId);
            var member = await memberRepo.GetByIdAsync(memberId);
            var scopeName = post.ScopeType switch
            {
                LeadershipPost.ScopeBatch => (await batchRepo.GetByIdAsync(post.ScopeId))?.Name,
                LeadershipPost.ScopeCommunity => (await communityRepo.GetByIdAsync(post.ScopeId))?.Name,
                _ => null,
            } ?? "the whole community";

            await postRepo.RemoveAsync(post);
            var who = member is null ? "A leader" : $"{member.FirstName} {member.LastName}".Trim();
            try
            {
                await auditRepo.AddAsync(new InstitutionAuditLogEntry
                {
                    ActorId = memberId, Actor = $"{who} (leader)", Action = "Leader Stepped Down",
                    Target = $"{who}: {role?.Name ?? "a removed role"}, {scopeName}", CreatedBy = memberId,
                });
            }
            catch (Exception e) { logger.LogWarning(e, "Could not record that {MemberId} stepped down", memberId); }
            return new object().ToOkApiResponse("You have stepped down");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error stepping down member {MemberId} from post {PostId}", memberId, postId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to step down");
        }
    }
}

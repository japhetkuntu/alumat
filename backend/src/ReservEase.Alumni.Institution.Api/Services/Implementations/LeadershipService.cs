using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class LeadershipService(
    IAlumniPgRepository<LeadershipPost> postRepo,
    IAlumniPgRepository<LeadershipRole> roleRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant,
    IInstitutionAuditLogService auditLog,
    ILogger<LeadershipService> logger) : ILeadershipService
{
    private const string InstitutionName = "The whole community";

    public IApiResponse<List<PrivilegeItem>> GetPrivileges()
        => LeadershipPrivileges.All.Select(p => new PrivilegeItem(p.Key, p.Label, p.Description)).ToList().ToOkApiResponse();

    // ── Settings ─────────────────────────────────────────────────────────────

    public async Task<IApiResponse<LeadershipSettingsItem>> GetSettingsAsync()
    {
        try
        {
            var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            return new LeadershipSettingsItem(GroupMessaging.Limit(institution?.GroupMessagesPerDay), GroupMessaging.MaxPerDay).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving leadership settings");
            return ApiResponseExtensions.ToServerErrorApiResponse<LeadershipSettingsItem>("Failed to retrieve the settings");
        }
    }

    public async Task<IApiResponse<LeadershipSettingsItem>> UpdateSettingsAsync(SaveLeadershipSettingsRequest request, AuthData admin)
    {
        try
        {
            if (request.GroupMessagesPerDay is < 1 or > GroupMessaging.MaxPerDay)
                return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipSettingsItem>($"Choose a number from 1 to {GroupMessaging.MaxPerDay}");
            var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            if (institution is null) return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipSettingsItem>("Institution not found");

            var before = GroupMessaging.Limit(institution.GroupMessagesPerDay);
            institution.GroupMessagesPerDay = request.GroupMessagesPerDay;
            institution.UpdatedAt = DateTime.UtcNow;
            institution.UpdatedBy = admin.Id;
            await institutionRepo.UpdateAsync(institution);
            if (before != request.GroupMessagesPerDay)
                await auditLog.LogAsync(admin, "Leader Message Limit Changed", $"{before} to {request.GroupMessagesPerDay} a day");
            return new LeadershipSettingsItem(request.GroupMessagesPerDay, GroupMessaging.MaxPerDay).ToOkApiResponse("Saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error saving leadership settings");
            return ApiResponseExtensions.ToServerErrorApiResponse<LeadershipSettingsItem>("Failed to save the settings");
        }
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<LeadershipRoleItem>>> GetRolesAsync()
    {
        try
        {
            var roles = (await roleRepo.GetAllAsync(_ => true)).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var holders = (await postRepo.GetAllAsync(_ => true)).GroupBy(p => p.RoleId).ToDictionary(g => g.Key, g => g.Count());
            return roles.Select(r => ToItem(r, holders.GetValueOrDefault(r.Id))).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving leadership roles");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<LeadershipRoleItem>>("Failed to retrieve roles");
        }
    }

    public async Task<IApiResponse<LeadershipRoleItem>> CreateRoleAsync(SaveLeadershipRoleRequest request, AuthData admin)
    {
        try
        {
            var (name, privileges, problem) = Clean(request);
            if (problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipRoleItem>(problem);
            if (await NameTakenAsync(name, null))
                return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipRoleItem>($"You already have a role called \"{name}\"");

            var role = new LeadershipRole { Name = name, Privileges = privileges, CreatedBy = admin.Id };
            await roleRepo.AddAsync(role);
            await auditLog.LogAsync(admin, "Leadership Role Created", $"{name}: {Describe(privileges)}");
            return ToItem(role, 0).ToOkApiResponse("Role created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating leadership role {Name}", request.Name);
            return ApiResponseExtensions.ToServerErrorApiResponse<LeadershipRoleItem>("Failed to create the role");
        }
    }

    public async Task<IApiResponse<LeadershipRoleItem>> UpdateRoleAsync(string id, SaveLeadershipRoleRequest request, AuthData admin)
    {
        try
        {
            var role = await roleRepo.GetByIdAsync(id);
            if (role is null) return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipRoleItem>("That role was not found");
            var (name, privileges, problem) = Clean(request);
            if (problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipRoleItem>(problem);
            if (await NameTakenAsync(name, id))
                return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipRoleItem>($"You already have a role called \"{name}\"");

            var before = $"{role.Name}: {Describe(role.Privileges)}";
            role.Name = name; role.Privileges = privileges; role.UpdatedAt = DateTime.UtcNow; role.UpdatedBy = admin.Id;
            await roleRepo.UpdateAsync(role);
            await auditLog.LogAsync(admin, "Leadership Role Updated", $"{before} -> {name}: {Describe(privileges)}");
            var holders = await postRepo.GetAllAsync(p => p.RoleId == id);
            return ToItem(role, holders.Count()).ToOkApiResponse("Role updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating leadership role {RoleId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<LeadershipRoleItem>("Failed to update the role");
        }
    }

    public async Task<IApiResponse<object>> DeleteRoleAsync(string id, AuthData admin)
    {
        try
        {
            var role = await roleRepo.GetByIdAsync(id);
            if (role is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That role was not found");
            var held = (await postRepo.GetAllAsync(p => p.RoleId == id)).Count();
            if (held > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(
                    $"{held} {(held == 1 ? "person holds" : "people hold")} this role. Remove {(held == 1 ? "them" : "them")} from it first.");

            await roleRepo.RemoveAsync(role);
            await auditLog.LogAsync(admin, "Leadership Role Deleted", role.Name);
            return new object().ToOkApiResponse("Role deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting leadership role {RoleId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete the role");
        }
    }

    // ── Posts ────────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<LeadershipPostItem>>> GetPostsAsync(string? scopeType, string? scopeId, string? memberId)
    {
        try
        {
            var posts = (await postRepo.GetAllAsync(p =>
                (string.IsNullOrEmpty(scopeType) || p.ScopeType == scopeType)
                && (string.IsNullOrEmpty(scopeId) || p.ScopeId == scopeId)
                && (string.IsNullOrEmpty(memberId) || p.MemberId == memberId))).OrderBy(p => p.CreatedAt).ToList();
            return (await ToItemsAsync(posts)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving leadership posts (scope={ScopeType}/{ScopeId}, member={MemberId})", scopeType, scopeId, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<LeadershipPostItem>>("Failed to retrieve leadership");
        }
    }

    public async Task<IApiResponse<LeadershipPostItem>> AppointAsync(AppointLeaderRequest request, AuthData admin)
    {
        try
        {
            if (!LeadershipPost.IsValidScope(request.ScopeType))
                return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipPostItem>("Choose a year group, a community or the whole community");

            var scopeId = request.ScopeType == LeadershipPost.ScopeInstitution ? string.Empty : request.ScopeId ?? string.Empty;
            if (request.ScopeType != LeadershipPost.ScopeInstitution && await ScopeNameAsync(request.ScopeType, scopeId) is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipPostItem>("That group was not found");

            var role = await roleRepo.GetByIdAsync(request.RoleId);
            if (role is null) return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipPostItem>("That role was not found");

            var member = await memberRepo.GetByIdAsync(request.MemberId);
            if (member is null || member.Status != "Active")
                return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipPostItem>("That member was not found, or is not active");

            var existing = await postRepo.GetOneAsync(p => p.MemberId == member.Id && p.ScopeType == request.ScopeType && p.ScopeId == scopeId && p.RoleId == role.Id);
            if (existing is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipPostItem>($"{member.FirstName} already holds that role here");

            var post = new LeadershipPost { MemberId = member.Id, RoleId = role.Id, ScopeType = request.ScopeType, ScopeId = scopeId, CreatedBy = admin.Id };
            await postRepo.AddAsync(post);

            var scopeName = await ScopeNameAsync(post.ScopeType, post.ScopeId) ?? InstitutionName;
            await auditLog.LogAsync(admin, "Leader Appointed", $"{member.FirstName} {member.LastName}: {role.Name}, {scopeName} ({Describe(role.Privileges)})");
            await TellAsync(member.Id, $"You're now {role.Name}",
                AppointmentBody(role, scopeName, post.ScopeType == LeadershipPost.ScopeInstitution),
                role.Privileges.Count > 0 ? "/manage" : "/profile", admin.Id);
            return (await ToItemsAsync([post])).Single().ToOkApiResponse("Appointed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error appointing leader {MemberId} to {ScopeType}/{ScopeId}", request.MemberId, request.ScopeType, request.ScopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<LeadershipPostItem>("Failed to appoint the leader");
        }
    }

    public async Task<IApiResponse<LeadershipPostItem>> ChangeRoleAsync(string id, string roleId, AuthData admin)
    {
        try
        {
            var post = await postRepo.GetByIdAsync(id);
            if (post is null) return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipPostItem>("That appointment was not found");
            var role = await roleRepo.GetByIdAsync(roleId);
            if (role is null) return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipPostItem>("That role was not found");
            var member = await memberRepo.GetByIdAsync(post.MemberId);
            if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<LeadershipPostItem>("That member was not found");

            if (post.RoleId == role.Id) return (await ToItemsAsync([post])).Single().ToOkApiResponse("Already has that role");
            var taken = await postRepo.GetOneAsync(p => p.MemberId == post.MemberId && p.ScopeType == post.ScopeType && p.ScopeId == post.ScopeId && p.RoleId == role.Id);
            if (taken is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<LeadershipPostItem>($"{member.FirstName} already holds {role.Name} here");

            var before = await roleRepo.GetByIdAsync(post.RoleId);
            post.RoleId = role.Id; post.UpdatedAt = DateTime.UtcNow; post.UpdatedBy = admin.Id;
            await postRepo.UpdateAsync(post);

            var scopeName = await ScopeNameAsync(post.ScopeType, post.ScopeId) ?? InstitutionName;
            await auditLog.LogAsync(admin, "Leader Role Changed",
                $"{member.FirstName} {member.LastName}: {before?.Name ?? "a removed role"} -> {role.Name}, {scopeName} ({Describe(role.Privileges)})");
            await TellAsync(member.Id, $"You're now {role.Name}",
                $"Your role for {(post.ScopeType == LeadershipPost.ScopeInstitution ? "the whole community" : scopeName)} changed from {before?.Name ?? "your earlier role"} to {role.Name}. "
                    + RoleChangeEffect(role, post.ScopeType == LeadershipPost.ScopeInstitution),
                role.Privileges.Count > 0 ? "/manage" : "/profile", admin.Id);
            return (await ToItemsAsync([post])).Single().ToOkApiResponse("Role changed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error changing role of leadership post {PostId} to {RoleId}", id, roleId);
            return ApiResponseExtensions.ToServerErrorApiResponse<LeadershipPostItem>("Failed to change the role");
        }
    }

    public async Task<IApiResponse<object>> RemoveAsync(string id, AuthData admin)
    {
        try
        {
            var post = await postRepo.GetByIdAsync(id);
            if (post is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That post was not found");

            var member = await memberRepo.GetByIdAsync(post.MemberId);
            var role = await roleRepo.GetByIdAsync(post.RoleId);
            var scopeName = await ScopeNameAsync(post.ScopeType, post.ScopeId) ?? InstitutionName;
            await postRepo.RemoveAsync(post);
            if (member is not null)
                await TellAsync(member.Id, $"You're no longer {role?.Name ?? "a leader"} for {scopeName}",
                    "Thank you for what you did. You are still a member, and nothing else about your account has changed.", "/profile", admin.Id);
            await auditLog.LogAsync(admin, "Leader Removed", $"{(member is null ? "A member" : $"{member.FirstName} {member.LastName}")}: {role?.Name ?? "a removed role"}, {scopeName}");
            return new object().ToOkApiResponse("Removed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error removing leadership post {PostId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to remove the post");
        }
    }

    // ── Telling the member ───────────────────────────────────────────────────

    /// <summary>What the appointment means, in plain words: what they can now do, or that the title is shown and changes nothing.</summary>
    private static string AppointmentBody(LeadershipRole role, string scopeName, bool wholeInstitution)
    {
        var where = wholeInstitution ? "for the whole community" : $"for {scopeName}";
        var labels = LeadershipPrivileges.All.Where(p => role.Privileges.Contains(p.Key)).Select(p => p.Label.ToLowerInvariant()).ToList();
        if (labels.Count == 0) return $"You've been named {role.Name} {where}. It is shown on your profile and doesn't change what you can access.";
        var can = labels.Count == 1 ? labels[0] : $"{string.Join(", ", labels.Take(labels.Count - 1))} and {labels[^1]}";
        return $"You've been named {role.Name} {where}. You can now {can}, in {(wholeInstitution ? "any group" : "that group")}.";
    }

    private static string RoleChangeEffect(LeadershipRole role, bool wholeInstitution)
    {
        var labels = LeadershipPrivileges.All.Where(p => role.Privileges.Contains(p.Key)).Select(p => p.Label.ToLowerInvariant()).ToList();
        if (labels.Count == 0) return "It is shown on your profile and doesn't give you any extra access.";
        var can = labels.Count == 1 ? labels[0] : $"{string.Join(", ", labels.Take(labels.Count - 1))} and {labels[^1]}";
        return $"You can now {can}, in {(wholeInstitution ? "any group" : "that group")}.";
    }

    private async Task TellAsync(string memberId, string title, string body, string actionUrl, string by)
    {
        try
        {
            await notificationRepo.AddAsync(new Notification
            {
                RecipientId = memberId, RecipientType = "Member", Title = title, Body = body, Type = "Leadership",
                RelatedEntityType = "Leadership", ActionUrl = actionUrl, CreatedBy = by,
            });
        }
        catch (Exception e)
        {
            // The appointment is already saved; a missed notice must not undo it.
            logger.LogWarning(e, "Could not notify member {MemberId} about a leadership change", memberId);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LeadershipRoleItem ToItem(LeadershipRole r, int holders) => new(r.Id, r.Name, r.Privileges.Where(LeadershipPrivileges.IsValid).ToList(), holders);

    private static string Describe(IEnumerable<string> privileges)
    {
        var labels = privileges.Select(k => LeadershipPrivileges.All.FirstOrDefault(p => p.Key == k)?.Label).Where(l => l is not null).ToList();
        return labels.Count == 0 ? "recognition only" : string.Join(", ", labels);
    }

    private static (string Name, List<string> Privileges, string? Problem) Clean(SaveLeadershipRoleRequest request)
    {
        var name = string.Join(" ", (request.Name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length == 0) return (name, [], "Give the role a name");
        var asked = (request.Privileges ?? []).Distinct().ToList();
        if (asked.Any(k => !LeadershipPrivileges.IsValid(k))) return (name, [], "One of those privileges is not recognised");
        // Kept in the order they are offered, so a role always lists its privileges the same way.
        return (name, LeadershipPrivileges.All.Select(p => p.Key).Where(asked.Contains).ToList(), null);
    }

    private async Task<bool> NameTakenAsync(string name, string? exceptId)
        => (await roleRepo.GetAllAsync(r => r.Name.ToLower() == name.ToLower() && r.Id != exceptId)).Any();

    private async Task<string?> ScopeNameAsync(string scopeType, string scopeId) => scopeType switch
    {
        LeadershipPost.ScopeInstitution => InstitutionName,
        LeadershipPost.ScopeBatch => (await batchRepo.GetByIdAsync(scopeId))?.Name,
        LeadershipPost.ScopeCommunity => (await communityRepo.GetByIdAsync(scopeId))?.Name,
        _ => null,
    };

    private async Task<List<LeadershipPostItem>> ToItemsAsync(List<LeadershipPost> posts)
    {
        if (posts.Count == 0) return [];
        var memberIds = posts.Select(p => p.MemberId).Distinct().ToList();
        var roleIds = posts.Select(p => p.RoleId).Distinct().ToList();
        var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);
        var roles = (await roleRepo.GetAllAsync(r => roleIds.Contains(r.Id))).ToDictionary(r => r.Id);
        var batches = (await batchRepo.GetAllAsync(_ => true)).ToDictionary(b => b.Id, b => b.Name);
        var communities = (await communityRepo.GetAllAsync(_ => true)).ToDictionary(c => c.Id, c => c.Name);

        return posts.Where(p => members.ContainsKey(p.MemberId) && roles.ContainsKey(p.RoleId)).Select(p =>
        {
            var m = members[p.MemberId];
            var r = roles[p.RoleId];
            var scopeName = p.ScopeType switch
            {
                LeadershipPost.ScopeBatch => batches.GetValueOrDefault(p.ScopeId),
                LeadershipPost.ScopeCommunity => communities.GetValueOrDefault(p.ScopeId),
                _ => InstitutionName,
            } ?? "A removed group";
            return new LeadershipPostItem(p.Id, p.MemberId, $"{m.FirstName} {m.LastName}".Trim(), m.ProfilePictureUrl, m.GraduationYear,
                r.Id, r.Name, r.Privileges.Where(LeadershipPrivileges.IsValid).ToList(), p.ScopeType, p.ScopeId, scopeName, p.CreatedAt);
        }).ToList();
    }
}

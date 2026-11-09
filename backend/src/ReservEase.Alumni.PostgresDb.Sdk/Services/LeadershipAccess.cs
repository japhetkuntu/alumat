using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>One role a member holds, with where it applies and what it allows.</summary>
public record HeldRole(string PostId, string RoleId, string RoleName, IReadOnlyList<string> Privileges, string ScopeType, string ScopeId);

/// <summary>
/// The one place that answers "may this member do this here?" for leadership roles. A privilege holds only inside the group the role
/// was given for; a role given for the whole institution holds everywhere. Everything that acts on a leader's behalf asks here, so the
/// rule can never differ between screens.
/// </summary>
public class LeadershipAccess(IAlumniPgRepository<LeadershipPost> postRepo, IAlumniPgRepository<LeadershipRole> roleRepo)
{
    public async Task<List<HeldRole>> GetHeldAsync(string memberId)
    {
        var posts = (await postRepo.GetAllAsync(p => p.MemberId == memberId)).ToList();
        if (posts.Count == 0) return [];
        var roleIds = posts.Select(p => p.RoleId).Distinct().ToList();
        var roles = (await roleRepo.GetAllAsync(r => roleIds.Contains(r.Id))).ToDictionary(r => r.Id);
        return posts.Where(p => roles.ContainsKey(p.RoleId)).Select(p =>
        {
            var r = roles[p.RoleId];
            return new HeldRole(p.Id, r.Id, r.Name, r.Privileges.Where(LeadershipPrivileges.IsValid).ToList(), p.ScopeType, p.ScopeId);
        }).ToList();
    }

    public static bool Allows(IEnumerable<HeldRole> held, string privilege, string scopeType, string scopeId)
        => held.Any(h => h.Privileges.Contains(privilege)
            && (h.ScopeType == LeadershipPost.ScopeInstitution || (h.ScopeType == scopeType && h.ScopeId == scopeId)));

    public async Task<bool> HasPrivilegeAsync(string memberId, string privilege, string scopeType, string scopeId)
        => Allows(await GetHeldAsync(memberId), privilege, scopeType, scopeId);
}

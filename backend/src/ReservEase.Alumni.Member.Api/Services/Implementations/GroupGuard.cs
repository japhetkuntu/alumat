using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>A year group or community a request names, once it is known to exist in this institution. Only these two can be acted on.</summary>
public sealed record GroupRef(string Type, string Id, string Name, Batch? Batch, Community? Community)
{
    /// <summary>Where something made for this group is aimed: a community's own id, or the year of a year group. Never both.</summary>
    public (string? CommunityId, List<int>? YearGroups) Audience => Batch is { } b ? (null, [b.Year]) : (Community!.Id, null);

    /// <summary>
    /// Whether an item is aimed at exactly this group and nobody else. A fundraiser for several year groups is not any one of them's to look at:
    /// its totals and givers belong to all of them, so a single group's leader must not see across the others.
    /// </summary>
    public bool IsExactly(string? communityId, List<int>? yearGroups)
        => Community is not null ? communityId == Community.Id && (yearGroups is null || yearGroups.Count == 0)
            : communityId == null && yearGroups is { Count: 1 } && yearGroups[0] == Batch!.Year;

    /// <summary>Whether an item aimed at (communityId, yearGroups) is one of this group's.</summary>
    public bool Owns(string? communityId, List<int>? yearGroups)
        => Community is not null ? communityId == Community.Id : communityId == null && yearGroups != null && yearGroups.Contains(Batch!.Year);
}

/// <summary>
/// The one doorway into acting on a group as a leader: the group must exist in this institution, and the member must hold a role with the
/// privilege in that exact group. Every leader action goes through here, so the rule can never differ between screens.
/// </summary>
public class GroupGuard(LeadershipAccess access, IAlumniPgRepository<Batch> batchRepo, IAlumniPgRepository<Community> communityRepo)
{
    public async Task<GroupRef?> FindAsync(string scopeType, string scopeId)
    {
        if (scopeType == LeadershipPost.ScopeBatch && await batchRepo.GetByIdAsync(scopeId) is { } b) return new(scopeType, b.Id, b.Name, b, null);
        if (scopeType == LeadershipPost.ScopeCommunity && await communityRepo.GetByIdAsync(scopeId) is { } c) return new(scopeType, c.Id, c.Name, null, c);
        return null;
    }

    /// <summary>The group, or the refusal to send back: not found, or found but this member does not hold the privilege there.</summary>
    public async Task<(GroupRef? Group, IApiResponse<T>? Refusal)> GuardAsync<T>(string memberId, string privilege, string scopeType, string scopeId)
    {
        var group = await FindAsync(scopeType, scopeId);
        if (group is null) return (null, ApiResponseExtensions.ToNotFoundApiResponse<T>("That group was not found"));
        if (!await access.HasPrivilegeAsync(memberId, privilege, group.Type, group.Id))
            return (null, ApiResponseExtensions.ToForbiddenApiResponse<T>("You don't have permission to do that for this group"));
        return (group, null);
    }

    /// <summary>Like <see cref="GuardAsync{T}"/>, for a screen that either of several privileges opens.</summary>
    public async Task<(GroupRef? Group, IApiResponse<T>? Refusal, HashSet<string> Held)> GuardAnyAsync<T>(string memberId, string[] privileges, string scopeType, string scopeId)
    {
        var group = await FindAsync(scopeType, scopeId);
        if (group is null) return (null, ApiResponseExtensions.ToNotFoundApiResponse<T>("That group was not found"), []);
        var held = await access.GetHeldAsync(memberId);
        var have = privileges.Where(p => LeadershipAccess.Allows(held, p, group.Type, group.Id)).ToHashSet();
        return have.Count == 0
            ? (null, ApiResponseExtensions.ToForbiddenApiResponse<T>("You don't have permission to do that for this group"), [])
            : (group, null, have);
    }
}

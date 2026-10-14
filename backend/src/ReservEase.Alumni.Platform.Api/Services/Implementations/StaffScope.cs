using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>What the signed-in platform user may see of owned things (institutions, leads).</summary>
public record StaffVisibility(bool Unrestricted, string? StaffId, IReadOnlySet<string> StaffIds)
{
    public static readonly StaffVisibility Everything = new(true, null, new HashSet<string>());

    /// <summary>Whether something owned by <paramref name="ownerStaffId"/> is in view. Unowned things are only in view when unrestricted.</summary>
    public bool CanSee(string? ownerStaffId) => Unrestricted || (ownerStaffId is not null && StaffIds.Contains(ownerStaffId));
}

public interface IStaffScope
{
    Task<StaffVisibility> GetAsync();
}

/// <summary>
/// Decides, once per request, how much of the platform the caller may see. Only the Representative role is narrowed: they see
/// what they and the people under them own. Every other platform role keeps exactly the access it already had, and a
/// SuperAdmin sees everything regardless of who it is assigned to.
/// </summary>
public class StaffScope(IHttpContextAccessor http, IAlumniPgRepository<PlatformStaff> staffRepo) : IStaffScope
{
    private StaffVisibility? cached;

    public async Task<StaffVisibility> GetAsync()
    {
        if (cached is not null) return cached;

        var user = http.HttpContext?.User;
        if (user is null || !user.IsInRole(PlatformStaffRoles.Representative))
            return cached = StaffVisibility.Everything;

        var me = user.GetAccount().Id;
        var all = (await staffRepo.GetAllAsync(_ => true)).ToList();
        var ids = new StaffHierarchy(all).SelfAndDescendants(me);
        return cached = new StaffVisibility(false, me, ids);
    }
}

using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Extensions;

/// <summary>
/// Three-tier institution-staff authorization: SuperAdmin and Admin are never
/// restricted (Admin's only limitation — being unable to manage other admins —
/// is enforced separately via [Authorize(Roles=...)] on InstitutionStaffController,
/// not here). Only ScopedAdmin is actually scoped, by year-group and/or community,
/// keyed off whichever dimension(s) the specific content type supports.
/// </summary>
public static class ScopeAuthorizationExtensions
{
    public static bool CanViewScopedItem(this AuthData admin, List<int>? itemYearGroups, string? itemCommunityId = null, string? createdBy = null)
    {
        if (admin.Role != StaffRoles.ScopedAdmin)
            return true;

        if (createdBy != null && createdBy == admin.Id)
            return true;

        if (itemCommunityId != null && admin.CommunityIds != null && admin.CommunityIds.Contains(itemCommunityId))
            return true;

        if (admin.YearGroups is null || itemYearGroups is null)
            return false;

        return itemYearGroups.Any(y => admin.YearGroups.Contains(y));
    }

    public static bool CanModifyScopedItem(this AuthData admin, List<int>? itemYearGroups, string? createdBy, string? itemCommunityId = null)
        => admin.CanViewScopedItem(itemYearGroups, itemCommunityId, createdBy);

    public static List<int>? ResolveYearGroupsForCreation(this AuthData admin, List<int>? requestedYearGroups)
    {
        if (admin.Role != StaffRoles.ScopedAdmin)
            return requestedYearGroups;

        return admin.YearGroups;
    }

    public static string? ResolveCommunityForCreation(this AuthData admin, string? requestedCommunityId)
    {
        if (admin.Role != StaffRoles.ScopedAdmin)
            return requestedCommunityId;

        if (requestedCommunityId != null && admin.CommunityIds != null && admin.CommunityIds.Contains(requestedCommunityId))
            return requestedCommunityId;

        return admin.CommunityIds is { Count: 1 } ? admin.CommunityIds[0] : null;
    }
}

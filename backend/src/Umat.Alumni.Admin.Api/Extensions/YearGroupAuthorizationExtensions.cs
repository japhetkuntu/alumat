using System.Collections.Generic;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Extensions;

public static class YearGroupAuthorizationExtensions
{
public static bool CanViewYearGroupScopedItem(this AuthData admin, List<int>? yearGroups, string? createdBy = null)
{
    if (admin.Role == "SuperAdmin")
        return true;

    if (createdBy != null && createdBy == admin.Id)
        return true;

    if (!admin.GraduationYear.HasValue)
        return false;

    return yearGroups != null && yearGroups.Contains(admin.GraduationYear.Value);
}

public static bool CanModifyYearGroupScopedItem(this AuthData admin, List<int>? yearGroups, string? createdBy)
{
    if (admin.Role == "SuperAdmin")
        return yearGroups == null || yearGroups.Count == 0 || createdBy == admin.Id;

    if (createdBy != null && createdBy == admin.Id)
        return true;

    if (!admin.GraduationYear.HasValue)
        return false;

    return yearGroups != null && yearGroups.Contains(admin.GraduationYear.Value);
}

public static List<int>? ResolveYearGroupsForCreation(this AuthData admin, List<int>? requestedYearGroups)
    {
        if (admin.Role == "SuperAdmin")
            return requestedYearGroups;

        if (!admin.GraduationYear.HasValue)
            return null;

        return new List<int> { admin.GraduationYear.Value };
    }
}

using System.Collections.Generic;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class ScopeAuthorizationExtensionsTests
{
    [Fact]
    public void SuperAdmin_AlwaysHasAccess()
    {
        var admin = new AuthData { Role = "SuperAdmin", Id = "a" };
        Assert.True(admin.CanViewScopedItem(new List<int> { 2026 }));
        Assert.True(admin.CanViewScopedItem(null, "community-1"));
        Assert.True(admin.CanModifyScopedItem(new List<int> { 2026 }, "someone-else"));
    }

    [Fact]
    public void Admin_IsNeverRestrictedByScope()
    {
        // Regular Admin has no YearGroups/CommunityIds configured at all — should
        // still have full access, unlike a ScopedAdmin in the same situation.
        var admin = new AuthData { Role = "Admin", Id = "a" };
        Assert.True(admin.CanViewScopedItem(new List<int> { 2026 }));
        Assert.True(admin.CanViewScopedItem(null, "community-1"));
        Assert.True(admin.CanModifyScopedItem(new List<int> { 2026 }, "someone-else"));
        Assert.True(admin.CanModifyScopedItem(null, null));
    }

    [Fact]
    public void ScopedAdmin_AllowedWhenYearGroupMatches()
    {
        var admin = new AuthData { Role = "ScopedAdmin", Id = "a", YearGroups = new List<int> { 2026 } };
        Assert.True(admin.CanViewScopedItem(new List<int> { 2026, 2027 }));
        Assert.False(admin.CanViewScopedItem(new List<int> { 2025 }));
    }

    [Fact]
    public void ScopedAdmin_AllowedWhenCommunityMatches()
    {
        var admin = new AuthData { Role = "ScopedAdmin", Id = "a", CommunityIds = new List<string> { "community-1" } };
        Assert.True(admin.CanViewScopedItem(null, "community-1"));
        Assert.False(admin.CanViewScopedItem(null, "community-2"));
    }

    [Fact]
    public void ScopedAdmin_AllowedForOwnCreatedItemRegardlessOfScope()
    {
        var admin = new AuthData { Role = "ScopedAdmin", Id = "a", YearGroups = new List<int> { 2026 } };
        Assert.True(admin.CanModifyScopedItem(new List<int> { 1999 }, "a"));
        Assert.False(admin.CanModifyScopedItem(new List<int> { 1999 }, "someone-else"));
    }

    [Fact]
    public void ScopedAdmin_DeniedWhenNoScopeConfiguredAtAll()
    {
        var admin = new AuthData { Role = "ScopedAdmin", Id = "a" };
        Assert.False(admin.CanViewScopedItem(new List<int> { 2026 }));
        Assert.False(admin.CanViewScopedItem(null, "community-1"));
    }

    [Fact]
    public void ResolveYearGroupsForCreation_ScopedAdminAlwaysGetsOwnScope_RegardlessOfRequest()
    {
        var admin = new AuthData { Role = "ScopedAdmin", YearGroups = new List<int> { 2026 } };
        var resolved = admin.ResolveYearGroupsForCreation(new List<int> { 1999 });
        Assert.Equal(new List<int> { 2026 }, resolved);
    }

    [Fact]
    public void ResolveYearGroupsForCreation_AdminAndSuperAdminPassRequestThrough()
    {
        var admin = new AuthData { Role = "Admin" };
        var superAdmin = new AuthData { Role = "SuperAdmin" };
        var requested = new List<int> { 1999 };
        Assert.Equal(requested, admin.ResolveYearGroupsForCreation(requested));
        Assert.Equal(requested, superAdmin.ResolveYearGroupsForCreation(requested));
    }
}

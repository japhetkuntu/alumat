using System.Collections.Generic;
using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Xunit;

namespace Umat.Alumni.Admin.Api.Tests;

public class YearGroupAuthorizationExtensionsTests
{
    [Theory]
    [InlineData("SuperAdmin", null, true)]
    [InlineData("Admin", 2026, true)]
    [InlineData("Admin", null, false)]
    public void CanViewYearGroupScopedItem_ReturnsExpected(string role, int? gradYear, bool expected)
    {
        var admin = new AuthData { Role = role, GraduationYear = gradYear };
        var allowed = admin.CanViewYearGroupScopedItem(new List<int> { 2026 });
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void CanModifyYearGroupScopedItem_AllowsSuperAdminWhenGlobalOrOwn()
    {
        var admin = new AuthData { Role = "SuperAdmin", GraduationYear = 2026, Id = "a" };
        Assert.True(admin.CanModifyYearGroupScopedItem(null, "x"));
        Assert.True(admin.CanModifyYearGroupScopedItem(new List<int>(), "x"));
        Assert.True(admin.CanModifyYearGroupScopedItem(new List<int> { 2026 }, "a"));
        Assert.False(admin.CanModifyYearGroupScopedItem(new List<int> { 2026 }, "b"));
    }

    [Fact]
    public void CanModifyYearGroupScopedItem_RejectsNonSuperAdminIfNotInYearGroup()
    {
        var admin = new AuthData { Role = "Admin", GraduationYear = 2026 };
        Assert.False(admin.CanModifyYearGroupScopedItem(new List<int> { 2025 }, null));
        Assert.True(admin.CanModifyYearGroupScopedItem(new List<int> { 2026 }, null));
    }
}

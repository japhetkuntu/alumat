using System.Security.Claims;
using ReservEase.Alumni.Common.Sdk.Extensions;

namespace ReservEase.Alumni.Common.Tests;

public class IdentityPrincipalExtensionsTests
{
    private static ClaimsPrincipal Principal(params (string type, string value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.type, c.value)), "test"));

    [Fact]
    public void Maps_every_standard_claim_onto_AuthData()
    {
        var auth = Principal(
            (ClaimTypes.NameIdentifier, "user-1"), (ClaimTypes.Email, "a@x.com"), (ClaimTypes.GivenName, "Ama"),
            (ClaimTypes.Surname, "Mensah"), ("picture", "https://pic"), (ClaimTypes.Role, "Admin"),
            ("year_group", "2018"), (ClaimTypes.MobilePhone, "0241234567")).GetAccount();

        Assert.Equal("user-1", auth.Id);
        Assert.Equal("a@x.com", auth.Email);
        Assert.Equal("Ama", auth.FirstName);
        Assert.Equal("Mensah", auth.LastName);
        Assert.Equal("Ama Mensah", auth.Name);
        Assert.Equal("https://pic", auth.ProfilePictureUrl);
        Assert.Equal("Admin", auth.Role);
        Assert.Equal(2018, auth.GraduationYear);
        Assert.Equal("0241234567", auth.MobileNumber);
    }

    [Fact]
    public void An_empty_principal_yields_empty_strings_and_nulls_rather_than_throwing()
    {
        var auth = Principal().GetAccount();
        Assert.Equal(string.Empty, auth.Id);
        Assert.Equal(string.Empty, auth.Email);
        Assert.Equal(string.Empty, auth.Role);
        Assert.Equal(string.Empty, auth.MobileNumber);
        Assert.Null(auth.ProfilePictureUrl);
        Assert.Null(auth.GraduationYear);
        Assert.Null(auth.YearGroups);
        Assert.Null(auth.CommunityIds);
        Assert.Equal(string.Empty, auth.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("20.5")]
    public void An_unparseable_year_group_claim_is_ignored(string value)
        => Assert.Null(Principal(("year_group", value)).GetAccount().GraduationYear);

    [Fact]
    public void Name_trims_when_only_one_part_is_present()
    {
        Assert.Equal("Ama", Principal((ClaimTypes.GivenName, "Ama")).GetAccount().Name);
        Assert.Equal("Mensah", Principal((ClaimTypes.Surname, "Mensah")).GetAccount().Name);
    }

    [Fact]
    public void Scoped_year_groups_skip_non_numeric_entries_and_keep_order()
    {
        var auth = Principal(("year_group_scope", "2020"), ("year_group_scope", "x"), ("year_group_scope", "2018")).GetAccount();
        Assert.Equal(new[] { 2020, 2018 }, auth.YearGroups);
    }

    [Fact]
    public void Scoped_year_groups_are_null_when_none_parse()
        => Assert.Null(Principal(("year_group_scope", "x")).GetAccount().YearGroups);

    [Fact]
    public void Scoped_communities_drop_blank_values()
    {
        var auth = Principal(("community_scope", "c1"), ("community_scope", " "), ("community_scope", "c2")).GetAccount();
        Assert.Equal(new[] { "c1", "c2" }, auth.CommunityIds);
    }

    [Fact]
    public void Scoped_communities_are_null_when_all_blank()
        => Assert.Null(Principal(("community_scope", "")).GetAccount().CommunityIds);

    [Fact]
    public void Graduation_year_is_independent_of_scoped_year_groups()
    {
        var auth = Principal(("year_group", "2015"), ("year_group_scope", "2020")).GetAccount();
        Assert.Equal(2015, auth.GraduationYear);
        Assert.Equal(new[] { 2020 }, auth.YearGroups);
    }
}

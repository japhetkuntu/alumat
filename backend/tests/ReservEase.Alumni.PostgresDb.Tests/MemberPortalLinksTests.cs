using Xunit;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>Every link we hand out for an institution's member portal comes from here.</summary>
public class MemberPortalLinksTests
{
    [Fact]
    public void An_institution_without_its_own_domain_is_reached_on_its_subdomain()
        => Assert.Equal("https://umat.alumunion.com", MemberPortalLinks.Url("umat", null, "alumunion.com"));

    [Fact]
    public void An_institution_on_its_own_domain_is_reached_there_not_on_the_subdomain()
        => Assert.Equal("https://alumni.umat.edu.gh", MemberPortalLinks.Url("umat", "alumni.umat.edu.gh", "alumunion.com"));

    [Theory]
    [InlineData("https://alumni.umat.edu.gh/")]
    [InlineData("http://alumni.umat.edu.gh")]
    [InlineData("  ALUMNI.umat.edu.gh  ")]
    [InlineData("alumni.umat.edu.gh/login?x=1")]
    [InlineData("alumni.umat.edu.gh.")]
    public void A_custom_domain_typed_loosely_is_reduced_to_its_host(string typed)
        => Assert.Equal("https://alumni.umat.edu.gh", MemberPortalLinks.Url("umat", typed, "alumunion.com"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_custom_domain_falls_back_to_the_subdomain(string? custom)
        => Assert.Equal("https://umat.alumunion.com", MemberPortalLinks.Url("umat", custom, "alumunion.com"));

    [Theory]
    [InlineData("umat", null)]
    [InlineData("umat", "")]
    [InlineData(null, "alumunion.com")]
    [InlineData("", "alumunion.com")]
    public void With_nothing_to_build_from_there_is_no_link_rather_than_a_wrong_one(string? slug, string? baseDomain)
    {
        Assert.Null(MemberPortalLinks.Url(slug, null, baseDomain));
        Assert.Equal(string.Empty, MemberPortalLinks.UrlOrEmpty(slug, null, baseDomain));
    }

    [Fact]
    public void A_custom_domain_alone_is_enough_even_without_a_base_domain()
        => Assert.Equal("https://alumni.umat.edu.gh", MemberPortalLinks.Url(null, "alumni.umat.edu.gh", null));

    [Fact]
    public void A_stray_dot_or_spaces_in_the_base_domain_are_tolerated()
        => Assert.Equal("https://umat.alumunion.com", MemberPortalLinks.Url(" umat ", null, " .alumunion.com. "));
}

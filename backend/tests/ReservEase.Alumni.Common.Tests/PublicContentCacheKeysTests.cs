using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;

namespace ReservEase.Alumni.Common.Tests;

public class PublicContentCacheKeysTests
{
    [Fact]
    public void Keys_have_the_stable_documented_shape()
    {
        Assert.Equal("public-content:news:i1", PublicContentCacheKeys.News("i1"));
        Assert.Equal("public-content:events:i1", PublicContentCacheKeys.Events("i1"));
        Assert.Equal("public-content:spotlights:i1", PublicContentCacheKeys.Spotlights("i1"));
        Assert.Equal("public-content:businesses:i1", PublicContentCacheKeys.Businesses("i1"));
        Assert.Equal("public-content:theme:i1", PublicContentCacheKeys.Theme("i1"));
    }

    [Fact]
    public void Keys_never_collide_across_content_types_or_institutions()
    {
        var keys = new[] { "a", "b" }.SelectMany(i => new[]
        {
            PublicContentCacheKeys.News(i), PublicContentCacheKeys.Events(i), PublicContentCacheKeys.Spotlights(i),
            PublicContentCacheKeys.Businesses(i), PublicContentCacheKeys.Theme(i),
        }).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Cache_configs_use_distinct_databases_and_expected_ttls()
    {
        var content = new PublicContentCacheConfig();
        var tenant = new TenantResolutionCacheConfig();
        Assert.NotEqual(content.DbNumber, tenant.DbNumber);
        Assert.Equal(TimeSpan.FromMinutes(10), content.DefaultExpiry);
        Assert.Equal(TimeSpan.FromSeconds(60), tenant.DefaultExpiry);
        Assert.Equal("PublicContentCache", content.Alias);
        Assert.Equal("TenantResolutionCache", tenant.Alias);
    }

    [Fact]
    public void Token_and_google_configs_have_sensible_defaults()
    {
        var bearer = new BearerTokenConfig();
        Assert.Equal(8, bearer.AccessTokenLifetime);
        Assert.Equal(30, bearer.RefreshTokenLifetime);
        Assert.Equal(string.Empty, new GoogleAuthConfig().ClientId);
    }
}

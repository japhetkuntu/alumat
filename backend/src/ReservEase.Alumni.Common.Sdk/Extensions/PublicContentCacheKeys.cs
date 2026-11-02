namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// The one place both Member.Api (reads/populates) and Institution.Api
/// (invalidates on admin writes) agree on how public-content cache keys are
/// built — keeping the two apps' independent cache calls from silently
/// drifting apart.
/// </summary>
public static class PublicContentCacheKeys
{
    public static string News(string institutionId) => $"public-content:news:{institutionId}";
    public static string Events(string institutionId) => $"public-content:events:{institutionId}";
    public static string Spotlights(string institutionId) => $"public-content:spotlights:{institutionId}";
    public static string Businesses(string institutionId) => $"public-content:businesses:{institutionId}";
    public static string Communities(string institutionId) => $"public-content:communities:{institutionId}";
    public static string Theme(string institutionId) => $"public-content:theme:{institutionId}";
    /// <summary>The published website page. Publishing, unpublishing and restoring all clear this one key.</summary>
    /// <summary>What the website shows from the institution's own services and store. Cleared whenever a service or product is created, changed or removed, so ticking "Show on website" is seen at once.</summary>
    public static string SiteServices(string institutionId) => $"public-content:site-services:{institutionId}";
    public static string SiteStore(string institutionId) => $"public-content:site-store:{institutionId}";
    public static string Site(string institutionId) => $"public-content:site:{institutionId}";
}

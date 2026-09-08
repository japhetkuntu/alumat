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
    public static string Theme(string institutionId) => $"public-content:theme:{institutionId}";
}

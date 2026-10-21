namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// Which records may be shown in full to people who are not signed in. The institution marks a record public, but only
/// institution-wide items qualify: anything meant for one group or some year groups is for those members, so it stays
/// private whatever was asked. Used on save (so the stored flag is honest) and on read (so a later change of audience
/// closes the item again).
/// </summary>
public static class PublicVisibility
{
    public static bool IsFullyPublic(bool isPublic, string? communityId, IReadOnlyCollection<int>? yearGroups)
        => isPublic && string.IsNullOrEmpty(communityId) && (yearGroups is null || yearGroups.Count == 0);
}

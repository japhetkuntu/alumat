namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>Engagement-based audience segments — mirrors Institution.Api's BroadcastService
/// exactly (same semantics, duplicated rather than shared since the two APIs don't share a
/// services layer). See PlatformBroadcastService.ApplyEngagementSegmentAsync.</summary>
public static class PlatformEngagementSegments
{
    public const string Dormant = "Dormant";
    public const string NoContributionsEver = "NoContributionsEver";
    public const string NoContributionToActiveFundraiser = "NoContributionToActiveFundraiser";
}

public class PlatformBroadcastFilter
{
    /// <summary>Required — which institution's members to target. Platform staff have no
    /// single tenant of their own, unlike an institution admin's implicit current tenant.</summary>
    public string InstitutionId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
    public string? EngagementSegment { get; set; }
}

public class SendPlatformBroadcastRequest : PlatformBroadcastFilter
{
    public string? Title { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Channels { get; set; } = new();
    /// <summary>Pre-uploaded via POST /uploads/image — plain JSON here rather than multipart,
    /// since Platform.Api already has a generic image-upload endpoint the frontend calls first.</summary>
    public string? ImageUrl { get; set; }
}

public record PlatformBroadcastResult(int RecipientCount, List<string> Channels);

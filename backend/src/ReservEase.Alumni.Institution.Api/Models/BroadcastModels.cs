using Microsoft.AspNetCore.Http;

namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>Engagement-based audience segments a broadcast can target, on top of the plain member-field
/// filters below. See BroadcastService.ApplyEngagementSegmentAsync for how each is computed.</summary>
public static class EngagementSegments
{
    /// <summary>Never logged in, or hasn't logged in for BroadcastService.DormantDaysThreshold days.</summary>
    public const string Dormant = "Dormant";
    /// <summary>Has never made a single successful contribution (dues, fundraiser, or otherwise) since joining.</summary>
    public const string NoContributionsEver = "NoContributionsEver";
    /// <summary>Hasn't contributed to any fundraiser that is currently open — the case that prompted this
    /// feature: members who've had a live fundraiser to give to and simply haven't. Empty (not "everyone")
    /// when the institution has no open fundraiser right now, since the segment is meaningless without one.</summary>
    public const string NoContributionToActiveFundraiser = "NoContributionToActiveFundraiser";
}

public class BroadcastFilter
{
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
    /// <summary>One of EngagementSegments, or null/empty for no engagement-based narrowing.</summary>
    public string? EngagementSegment { get; set; }

    /// <summary>A saved audience (see SegmentsController). When set it replaces the other filters.</summary>
    public string? SegmentId { get; set; }
}

/// <summary>
/// Flat, not nested — this is bound with [FromForm] (an image upload needs multipart, not JSON), and the
/// frontend's toFormData() helper only serializes flat objects, so the filter fields that used to live
/// under a nested BroadcastFilter are inlined here directly instead.
/// </summary>
public class SendBroadcastRequest
{
    public string? Title { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Channels { get; set; } = new();
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
    public string? EngagementSegment { get; set; }
    public string? SegmentId { get; set; }
    /// <summary>Shown in the in-app notification panel and, when Email is a selected channel, as a
    /// banner image in the email — optional, purely to make a re-engagement push more eye-catching.</summary>
    public IFormFile? Image { get; set; }

    public BroadcastFilter ToFilter() => new()
    {
        Status = Status, DepartmentId = DepartmentId,
        GraduationYearFrom = GraduationYearFrom, GraduationYearTo = GraduationYearTo,
        EngagementSegment = EngagementSegment, SegmentId = SegmentId,
    };
}

public record BroadcastResult(int RecipientCount, List<string> Channels);

/// <summary>Which broadcast channels the platform has switched on for this institution. In-app is free, so it is always on.</summary>
public record BroadcastChannelsDto(bool InApp, bool Email, bool Sms, bool WhatsApp);

/// <summary>
/// The rule for what an institution may broadcast on. SMS and email cost the platform money, so each is off until platform
/// staff switch it on for that institution; in-app costs nothing and is never blocked.
/// </summary>
public static class BroadcastChannelRules
{
    public const string InApp = "InApp";
    public const string Email = "Email";
    public const string Sms = "Sms";
    public const string WhatsApp = "WhatsApp";

    /// <summary>The requested channels this institution is not allowed to use, by their display names. Empty when all are fine.</summary>
    public static List<string> Blocked(IEnumerable<string> requested, bool emailAllowed, bool smsAllowed, bool whatsAppAllowed)
    {
        var blocked = new List<string>();
        foreach (var c in requested.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (c.Equals(Email, StringComparison.OrdinalIgnoreCase) && !emailAllowed) blocked.Add("email");
            else if (c.Equals(Sms, StringComparison.OrdinalIgnoreCase) && !smsAllowed) blocked.Add("SMS");
            else if (c.Equals(WhatsApp, StringComparison.OrdinalIgnoreCase) && !whatsAppAllowed) blocked.Add("WhatsApp");
        }
        return blocked;
    }
}

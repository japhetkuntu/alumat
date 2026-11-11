using System.ComponentModel.DataAnnotations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>A year group or community the signed-in member may act on, with the roles that give them that and what they may do there.</summary>
/// <param name="PendingCount">How many people are waiting for this leader to approve them; 0 when the roles don't include approving.</param>
public record ManageGroupDto(string ScopeType, string ScopeId, string Name, List<string> RoleNames, List<string> Privileges, int PendingCount = 0, bool? AutoApproveMembers = null);

/// <param name="AutoApproveMembers">Whether anyone who asks to join is let in without waiting for a leader.</param>
public record SetAutoApproveRequest(bool AutoApproveMembers);

public record GroupSummaryDto(
    string Name, int TotalMembers, int NewLast30Days, int ActiveLast30Days, int ProfileCompletionPercent, int PendingRequests);

/// <summary>A person in the group. A waiting request can always be approved; only a community's can be declined (a year group's registration is declined by administrators).</summary>
public record GroupMemberDto(string MemberId, string Name, string? ProfilePictureUrl, string? JobTitle, string Status, DateTime JoinedAt, bool CanDecide, bool CanDecline);

public class DecideRequest
{
    public bool Approve { get; set; }
}

public class GroupMessageRequest
{
    [Required, MaxLength(500)] public string Text { get; set; } = string.Empty;
    /// <summary>Extra ways to reach people, beyond the notification and the phone alert that always go: Email and/or Sms. Only those the platform has switched on for the community.</summary>
    public List<string>? Channels { get; set; }
}

public record GroupMessageResult(int Recipients);

/// <summary>
/// What a leader needs to know before messaging the group: which extra ways of reaching people the platform has switched on (a notification and a
/// phone alert always go), how many people will receive it, and how many messages they have left today.
/// </summary>
public record ManageChannelsDto(bool Email, bool Sms, int Recipients = 0, int LeftToday = 0, int PerDay = GroupMessaging.DefaultPerDay);

/// <summary>One message already sent to a group: who sent it, the words, and how many people it reached.</summary>
/// <summary>How many are in a group and how many are waiting to be let in.</summary>
public record GroupPeopleCountsDto(int Members, int Waiting);

/// <summary>
/// What each section of a group's management area holds, for the numbers on its tabs. A section the leader's roles don't include is null,
/// so it has no number and no tab.
/// </summary>
public record ManageCountsDto(int? Members, int? Waiting, int? Events, int? News, int? Jobs, int? Links, int? Fundraisers);

public record GroupMessageHistoryDto(string Id, string SenderName, string Text, int Recipients, DateTime SentAt, bool IsMine);

public record GroupEventDto(string Id, string Title, string? Description, string Venue, DateTime StartDate, DateTime? EndDate, int? Capacity, int RsvpCount, string Status, bool CanEdit,
    string? BannerImageUrl = null, List<string>? ImageUrls = null, List<string>? YoutubeVideoUrls = null, string? GoogleLocationUrl = null);

public class SaveGroupEventRequest : GroupMediaFields
{
    /// <summary>A map or meeting link (Google Maps, Zoom, Meet, Teams).</summary>
    public string? GoogleLocationUrl { get; set; }
    [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required, MaxLength(200)] public string Venue { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Capacity { get; set; }
}

/// <summary>The pictures and videos a leader can attach. Sent as a form, because pictures travel as files.</summary>
public class GroupMediaFields
{
    public IFormFile? BannerImage { get; set; }
    /// <summary>Take away the picture the post already has (when no new one is sent).</summary>
    public bool RemoveBanner { get; set; }
    public List<IFormFile>? Images { get; set; }
    /// <summary>The pictures of an edited post to keep. Anything it did not already have is ignored.</summary>
    public List<string>? ExistingImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
}

// ── Content a leader makes for their group ──────────────────────────────────

public record GroupNewsDto(string Id, string Title, string Content, string Category, DateTime? PublishedAt, bool CanEdit, List<string>? ImageUrls = null, List<string>? YoutubeVideoUrls = null);

public class SaveGroupNewsRequest : GroupMediaFields
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [Required] public string Content { get; set; } = string.Empty;
    [MaxLength(40)] public string? Category { get; set; }
}

public record GroupJobDto(string Id, string Title, string Company, string Location, string Type, string? Description, string? ApplyUrl, DateTime? Deadline, string Status, bool CanEdit, string? BannerImageUrl = null);

public class SaveGroupJobRequest : GroupMediaFields
{
    [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string Company { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string Location { get; set; } = string.Empty;
    /// <summary>Full-time, Part-time, Contract or Internship.</summary>
    public string Type { get; set; } = "Full-time";
    public string? Description { get; set; }
    public string? ApplyUrl { get; set; }
    public DateTime? Deadline { get; set; }
}

public record GroupResourceDto(string Id, string Title, string? Description, string Category, string ExternalUrl, bool CanEdit, string? BannerImageUrl = null);

public class SaveGroupResourceRequest : GroupMediaFields
{
    [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [MaxLength(60)] public string? Category { get; set; }
    [Required] public string ExternalUrl { get; set; } = string.Empty;
}

// ── Fundraisers ─────────────────────────────────────────────────────────────

/// <summary>A fundraiser of the group. Only what a leader is meant to see: no fees, no net amounts, no payment details.</summary>
public record GroupCampaignDto(
    string Id, string Title, string? Description, decimal TargetAmount, decimal AmountPerMember, DateTime Deadline, string Status,
    decimal CollectedAmount, int PaidCount, bool CanEdit, bool CanSeeGiving, string? BannerImageUrl = null, string? YoutubeVideoUrl = null);

public class SaveGroupCampaignRequest : GroupMediaFields
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    /// <summary>What each member is asked to give. Fixed once someone has given.</summary>
    public decimal AmountPerMember { get; set; }
    public DateTime Deadline { get; set; }
}

public record GroupGiftDto(string GiverName, string? GiverPhotoUrl, decimal Amount, string Status, string Method, DateTime GivenAt);

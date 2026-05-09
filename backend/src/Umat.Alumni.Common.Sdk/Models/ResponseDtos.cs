namespace Umat.Alumni.Common.Sdk.Models;

public class JobDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ApplyUrl { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<int>? YearGroups { get; set; }
    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AlumniEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public int RsvpCount { get; set; }
    public bool IsTicketed { get; set; }
    public decimal? TicketPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<int>? YearGroups { get; set; }
    public string? GoogleLocationUrl { get; set; }
    public string? BannerImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NewsPostDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
    public List<int>? YearGroups { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? BannerImageUrl { get; set; }
    public int DownloadCount { get; set; }
    public List<int>? YearGroups { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CampaignDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal AmountPerMember { get; set; }
    public decimal? PensionerAmountPerMember { get; set; }
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal CollectedAmount { get; set; }
    public int PaidCount { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? YoutubeVideoUrl { get; set; }
    public bool IsPaystackDisbursed { get; set; }
    public DateTime? PaystackDisbursedAt { get; set; }
    public string? PaystackDisbursedBy { get; set; }
    public bool AllowOnlinePayments { get; set; }
    public bool AllowManualPayments { get; set; }
    public bool IsMembershipCampaign { get; set; }
    public int? MembershipYear { get; set; }
    public int? TotalEligibleMembers { get; set; }
    public ManualPaymentBankAccountDto? BankAccount { get; set; }
    public ManualPaymentMobileMoneyAccountDto? MobileMoneyAccount { get; set; }
    public List<int>? YearGroups { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ManualPaymentBankAccountDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}

public class ManualPaymentMobileMoneyAccountDto
{
    public string MobileMoneyNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}

public class ContributionDto
{
    public string Id { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string? CampaignTitle { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public string? MemberEmail { get; set; }
    public string? MemberNumber { get; set; }
    public string? MemberProfilePictureUrl { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionRef { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaystackDisbursementSummaryDto
{
    public decimal TotalPaidToPaystack { get; set; }
    public decimal TotalDisbursed { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ConfirmedCount { get; set; }
    public int DisbursedCount { get; set; }
}

public class ReportSummaryDto
{
    public int TotalMembers { get; set; }
    public int TotalContributions { get; set; }
    public decimal TotalCollected { get; set; }
    public int TotalCampaigns { get; set; }
    public int ActiveCampaigns { get; set; }
    public int ClosedCampaigns { get; set; }
    public int TotalEvents { get; set; }
    public int TotalJobs { get; set; }
}

public class MentorProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public string? MemberProfilePictureUrl { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public int MaxMentees { get; set; }
    public int CurrentMenteeCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<int>? YearGroups { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MentorshipRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string MentorProfileId { get; set; } = string.Empty;
    public string? MentorProfileName { get; set; }
    public string MenteeId { get; set; } = string.Empty;
    public string? MenteeName { get; set; }
    public string? MenteeProfilePictureUrl { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ForumCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ForumThreadDto
{
    public string Id { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public string? AuthorProfilePictureUrl { get; set; }
    public bool IsPinned { get; set; }
    public bool IsClosed { get; set; }
    public int ReplyCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ForumPostDto
{
    public string Id { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public string? AuthorProfilePictureUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class EventRsvpDto
{
    public string Id { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string? EventTitle { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public string? MemberEmail { get; set; }
    public string? MemberProfilePictureUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DirectoryMemberDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int GraduationYear { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? Company { get; set; }
    public string? JobTitle { get; set; }
    public string? Location { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Bio { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? MemberNumber { get; set; }
}

// ── Year Group Leaderboard ──────────────────────────────────────────────────

public class YearGroupLeaderboardEntryDto
{
    public int YearGroup { get; set; }
    public int TotalMembers { get; set; }
    public int MembershipPaidCount { get; set; }
    public decimal MembershipRate { get; set; }
    public decimal TotalContributed { get; set; }
    public int EventAttendanceCount { get; set; }
}

// ── Member Badges ───────────────────────────────────────────────────────────

public class MemberBadgeDto
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public string BadgeType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EarnedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Spotlight ───────────────────────────────────────────────────────────────

public class SpotlightDto
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public string? MemberProfilePictureUrl { get; set; }
    public int? MemberGraduationYear { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? FeaturedMonth { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Referral ────────────────────────────────────────────────────────────────

public class ReferralDto
{
    public string Id { get; set; } = string.Empty;
    public string ReferrerId { get; set; } = string.Empty;
    public string? ReferrerName { get; set; }
    public string ReferredEmail { get; set; } = string.Empty;
    public string? ReferredMemberId { get; set; }
    public string? ReferredMemberName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ── Class Notes ─────────────────────────────────────────────────────────────

public class ClassNoteDto
{
    public string Id { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public string? AuthorProfilePictureUrl { get; set; }
    public int YearGroup { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int LikeCount { get; set; }
    public bool IsLikedByMe { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Notification Preferences ────────────────────────────────────────────────

public class NotificationPreferenceDto
{
    public string Id { get; set; } = string.Empty;
    public bool MembershipReminders { get; set; }
    public bool CampaignAlerts { get; set; }
    public bool EventReminders { get; set; }
    public bool JobAlerts { get; set; }
    public bool ClassNoteAlerts { get; set; }
    public bool SpotlightAlerts { get; set; }
}

// ── In-App Notification ─────────────────────────────────────────────────────

public class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

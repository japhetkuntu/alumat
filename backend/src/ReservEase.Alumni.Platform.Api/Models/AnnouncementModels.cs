using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public static class NotificationChannels
{
    public const string InApp = "InApp";
    public const string Email = "Email";
    public const string Sms = "Sms";

    public static readonly string[] All = [InApp, Email, Sms];
}

public class SendAnnouncementRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Body { get; set; } = string.Empty;
    /// <summary>Kept only so an old client still gets a sensible label; ignored once RecipientStaffIds or InstitutionId is set.</summary>
    public string Audience { get; set; } = "All institutions";
    /// <summary>Any of "InApp", "Email", "Sms". Defaults to in-app only.</summary>
    public List<string> Channels { get; set; } = [NotificationChannels.InApp];
    /// <summary>Specific institution staff to notify, by id, regardless of which institution they belong to. Takes priority over InstitutionId.</summary>
    public List<string> RecipientStaffIds { get; set; } = [];
    /// <summary>When RecipientStaffIds is empty: every non-disabled admin of this one institution. Leave both empty for every institution's admins (the original "All institutions" behaviour).</summary>
    public string? InstitutionId { get; set; }
}

public record AnnouncementResponse(
    string Id, string Title, string Body, string Audience,
    DateTime SentAt, int SeenByAdmins, int TotalAdmins,
    List<string> Channels, int EmailSent, int SmsSent, int SmsSkippedNoPhone);

/// <summary>One institution admin, for the recipient picker — carries the institution's name since the picker searches across every institution.</summary>
public record StaffDirectoryEntry(
    string Id, string FirstName, string LastName, string Email, string Role, bool HasPhone,
    string InstitutionId, string InstitutionName);

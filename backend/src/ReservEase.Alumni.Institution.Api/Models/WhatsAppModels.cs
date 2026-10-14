namespace ReservEase.Alumni.Institution.Api.Models;

public record WhatsAppGroupDto(
    string Id, string Name, string? GroupJid, string Status, string? Code, DateTime? CodeExpiresAt,
    string? CommunityId, string? CommunityName, bool AssistantEnabled, List<string> AllowedCommands, bool PublishingEnabled,
    DateTime CreatedAt, DateTime? LinkedAt, string? InviteLink = null);

/// <summary>Everything the portal's WhatsApp tab needs in one read.</summary>
public record WhatsAppOverviewDto(
    bool PlatformEnabled, string? AssistantNumber, string? AssistantLink, List<WhatsAppGroupDto> Groups, List<string> Commands,
    /// <summary>AlumUnion has switched WhatsApp off for this institution. The institution cannot turn it back on.</summary>
    bool DeactivatedByPlatform = false);

public record CreateWhatsAppGroupRequest(string Name, string? CommunityId);

/// <param name="InviteLink">The group's own invitation link, shown to members of its community so they can join. Null leaves it as it is; an empty string removes it.</param>
public record UpdateWhatsAppGroupRequest(string? Name, bool AssistantEnabled, List<string>? AllowedCommands, bool PublishingEnabled, string? InviteLink = null);

public record WhatsAppPublishRequest(string Type, string ContentId, string GroupId, string? Text);

public record WhatsAppPublishPreviewDto(string Title, string Text, string Link);

/// <summary>
/// How far WhatsApp has reached among this institution's members. Counts only, no money and no numbers. "Reached by WhatsApp"
/// is each alert that was delivered there rather than by SMS.
/// </summary>
public record WhatsAppAdoptionDto(
    int ActiveMembers, int Connected, int AlertsOn, int ConnectedThisMonth,
    List<WhatsAppUsageMonthDto> Months,
    List<WhatsAppReferralChannelDto> ReferralsLast90Days);

public record WhatsAppUsageMonthDto(string Month, int ByWhatsApp, int BySms, int SmsAfterWhatsAppFailure);

public record WhatsAppReferralChannelDto(string Channel, int Invited, int Registered);

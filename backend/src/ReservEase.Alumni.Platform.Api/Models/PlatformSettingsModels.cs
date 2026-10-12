namespace ReservEase.Alumni.Platform.Api.Models;

/// <param name="WhatsAppBotEnabled">The platform switch for the WhatsApp bot (assistant, groups and WhatsApp notifications).</param>
/// <param name="WhatsAppConfigured">The channel is set up in configuration. Without it the switch has no effect.</param>
public record PlatformSettingsResponse(bool BlockOverdueCampaignPayments, bool WhatsAppBotEnabled = true, bool WhatsAppConfigured = false);

/// <param name="WhatsAppBotEnabled">Null leaves the switch as it is, so callers that only know the older setting do not change it.</param>
public record UpdatePlatformSettingsRequest(bool BlockOverdueCampaignPayments, bool? WhatsAppBotEnabled = null);

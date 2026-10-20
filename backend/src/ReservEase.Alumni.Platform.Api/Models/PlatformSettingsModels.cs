namespace ReservEase.Alumni.Platform.Api.Models;

public record PlatformSettingsResponse(bool BlockOverdueCampaignPayments);

public record UpdatePlatformSettingsRequest(bool BlockOverdueCampaignPayments);

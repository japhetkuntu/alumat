namespace ReservEase.Alumni.Platform.Api.Models;

public record IntegrationKeyItem(string Id, string Name, string KeyPrefix, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? RevokedAt);

public record IntegrationSettingsResponse(
    bool Enabled, string PortalName, string RedirectUrl, string BannerMessage, int BannerHours, int LinkExpiryHours, int StatusCacheDays,
    List<IntegrationKeyItem> Keys, int LinksIssued, int LinksUsed);

public record UpdateIntegrationRequest(
    bool Enabled, string? PortalName, string? RedirectUrl, string? BannerMessage, int BannerHours, int LinkExpiryHours, int StatusCacheDays);

public record CreateIntegrationKeyRequest(string? Name);

/// <summary>The only time the full key is ever returned.</summary>
public record CreatedIntegrationKeyResponse(IntegrationKeyItem Key, string ApiKey);

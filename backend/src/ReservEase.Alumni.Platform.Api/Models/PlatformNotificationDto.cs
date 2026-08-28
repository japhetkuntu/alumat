namespace ReservEase.Alumni.Platform.Api.Models;

public record PlatformNotificationDto(
    string Id, string Title, string Body, string Type, bool IsRead, DateTime? ReadAt,
    string? RelatedEntityId, string? RelatedEntityType, string? ActionUrl, DateTime CreatedAt);

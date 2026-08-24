namespace ReservEase.Alumni.Platform.Api.Models;

public record AuditLogEntryResponse(string Id, string Actor, string Action, string Target, DateTime Timestamp);

namespace ReservEase.Alumni.Institution.Api.Models;

public record InstitutionAuditLogEntryResponse(string Id, string Actor, string Action, string Target, DateTime Timestamp);

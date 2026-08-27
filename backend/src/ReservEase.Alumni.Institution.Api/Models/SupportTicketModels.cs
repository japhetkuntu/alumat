using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public class CreateSupportTicketRequest
{
    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    [Required]
    public string Message { get; set; } = string.Empty;
}

public record SupportTicketResponse(
    string Id, string Subject, string Severity, string Status,
    string Message, string? InternalNote, DateTime CreatedAt, DateTime? UpdatedAt);

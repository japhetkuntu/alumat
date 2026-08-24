using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

public class SendAnnouncementRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Body { get; set; } = string.Empty;
    public string Audience { get; set; } = "All institutions";
}

public record AnnouncementResponse(
    string Id, string Title, string Body, string Audience,
    DateTime SentAt, int SeenByAdmins, int TotalAdmins);

namespace ReservEase.Alumni.Member.Api.Models;

public record RegisterAsMentorRequest(
    string Area, string? Bio, int MaxMentees = 3,
    string? ContactLinkedInUrl = null, string? ContactWhatsAppNumber = null, string? ContactPhoneNumber = null);
public record RequestMentorshipRequest(string MentorProfileId, string Area, string? Message);

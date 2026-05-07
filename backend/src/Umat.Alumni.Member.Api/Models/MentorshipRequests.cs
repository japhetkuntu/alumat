namespace Umat.Alumni.Member.Api.Models;

public record RegisterAsMentorRequest(string Area, string? Bio, int MaxMentees = 3);
public record RequestMentorshipRequest(string MentorProfileId, string Area, string? Message);

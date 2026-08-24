namespace ReservEase.Alumni.Institution.Api.Models;

public record ApproveMentorRequest(string MentorProfileId);
public record RespondToMentorshipRequest(string RequestId, string Status);

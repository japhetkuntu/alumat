namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>
/// The answer to "is this student ID already a member?". When it is not, <paramref name="RegistrationUrl"/> is a one-use link for
/// that student, which registers and is approved straight away, and <paramref name="LinkExpiresAt"/> says until when it works.
/// </summary>
public record IntegrationMemberStatusResponse(bool Exists, string? RegistrationUrl, DateTime? LinkExpiresAt);

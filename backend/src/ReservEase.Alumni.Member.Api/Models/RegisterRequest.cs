namespace ReservEase.Alumni.Member.Api.Models;

public record RegisterRequest(
    string FirstName, string LastName, string Email, string Password,
    string Phone, string StudentId, int GraduationYear, string? DepartmentId,
    string? ReferralCode = null, string? Program = null);

/// <summary>
/// Registration via a verified Google identity — skips the OTP step entirely
/// (Google has already proven the email is real and reachable) and skips a
/// password (this member will only ever sign in with Google). Everything
/// Google can't supply — phone, student ID, graduation year, department —
/// still comes from the form, same as normal registration.
/// </summary>
public record GoogleRegisterRequest(
    string IdToken, string Phone, string StudentId, int GraduationYear, string? DepartmentId,
    string? ReferralCode = null, string? Program = null);

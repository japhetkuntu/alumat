namespace Umat.Alumni.Member.Api.Models;

public record RegisterRequest(
    string FirstName, string LastName, string Email, string Password,
    string Phone, string StudentId, int GraduationYear, string? DepartmentId,
    string? ReferralCode = null);

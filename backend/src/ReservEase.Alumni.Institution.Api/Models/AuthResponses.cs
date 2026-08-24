namespace ReservEase.Alumni.Institution.Api.Models;

public record AuthUserResponse(string Id, string Email, string FirstName, string LastName, string Role, int? GraduationYear, string? ProfilePictureUrl);
public record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public record InstitutionTokenResponse(AuthUserResponse User, AuthTokensResponse Tokens);
public record InstitutionStaffProfileResponse(string Id, string FirstName, string LastName, string Email, string Role, int? GraduationYear, string? ProfilePictureUrl);

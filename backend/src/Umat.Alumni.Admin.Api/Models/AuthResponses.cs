namespace Umat.Alumni.Admin.Api.Models;

public record AuthUserResponse(string Id, string Email, string FirstName, string LastName, string Role, int? GraduationYear, string? ProfilePictureUrl);
public record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public record AdminTokenResponse(AuthUserResponse User, AuthTokensResponse Tokens);
public record AdminProfileResponse(string Id, string FirstName, string LastName, string Email, string Role, int? GraduationYear, string? ProfilePictureUrl);

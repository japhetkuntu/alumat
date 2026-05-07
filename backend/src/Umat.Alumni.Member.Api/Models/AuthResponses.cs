namespace Umat.Alumni.Member.Api.Models;

public record AuthUserResponse(string Id, string Email, string FirstName, string LastName, string Role, int GraduationYear, string? ProfilePictureUrl);
public record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public record MemberTokenResponse(AuthUserResponse User, AuthTokensResponse Tokens);

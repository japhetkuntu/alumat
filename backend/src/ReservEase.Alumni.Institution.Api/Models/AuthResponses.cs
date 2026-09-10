namespace ReservEase.Alumni.Institution.Api.Models;

public record AuthUserResponse(string Id, string Email, string FirstName, string LastName, string Role, List<int>? YearGroups, List<string>? CommunityIds, string? ProfilePictureUrl);
// Token strings never leave the server in the response body — they travel only as
// httpOnly cookies (see AuthCookieExtensions). ExpiresIn stays here since it isn't
// a secret and the frontend uses it to schedule a proactive refresh.
public record AuthTokensResponse(int ExpiresIn);
public record InstitutionTokenResponse(AuthUserResponse User, AuthTokensResponse Tokens);
public record InstitutionStaffProfileResponse(string Id, string FirstName, string LastName, string Email, string Role, List<int>? YearGroups, List<string>? CommunityIds, string? ProfilePictureUrl);

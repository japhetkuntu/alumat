using System.Security.Claims;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

public static class IdentityPrincipalExtensions
{
    private const string PictureClaimType = "picture";

    public static AuthData GetAccount(this ClaimsPrincipal principal)
    {
        var yearGroupClaim = principal.FindFirst("year_group")?.Value;
        int? graduationYear = null;
        if (!string.IsNullOrWhiteSpace(yearGroupClaim) && int.TryParse(yearGroupClaim, out var parsedYear))
        {
            graduationYear = parsedYear;
        }

        var scopedYearGroups = principal.FindAll("year_group_scope")
            .Select(c => int.TryParse(c.Value, out var y) ? (int?)y : null)
            .Where(y => y.HasValue)
            .Select(y => y!.Value)
            .ToList();

        var scopedCommunityIds = principal.FindAll("community_scope")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        return new AuthData
        {
            Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            FirstName = principal.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty,
            LastName = principal.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty,
            ProfilePictureUrl = principal.FindFirst(PictureClaimType)?.Value,
            Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty,
            GraduationYear = graduationYear,
            MobileNumber = principal.FindFirst(ClaimTypes.MobilePhone)?.Value ?? string.Empty,
            YearGroups = scopedYearGroups.Count > 0 ? scopedYearGroups : null,
            CommunityIds = scopedCommunityIds.Count > 0 ? scopedCommunityIds : null,
        };
    }
}

namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>An administrator as the structure view needs them: who they are and what they are responsible for.</summary>
public record StructureAdminDto(
    string Id, string FirstName, string LastName, string Name, string Email, string? Phone, string Role, bool IsDisabled,
    List<int> YearGroups, List<string> CommunityIds, DateTime? LastLoginAt);

/// <summary>One graduating-class group ("batch", or whatever the institution calls it) with how many active members it holds.</summary>
public record StructureCohortDto(string Id, string Name, int Year, bool IsActive, int MemberCount);

public record StructureLeaderDto(string MemberId, string Name);

/// <summary>One community with its size and the members who lead it.</summary>
public record StructureCommunityDto(string Id, string Name, bool IsActive, int MemberCount, int PendingCount, List<StructureLeaderDto> Leaders);

/// <summary>
/// The whole institution in one read, so the structure page can draw it without a request per node. Counts only for members:
/// no member list. "Unassigned" is worked out here so the page and any other client agree.
/// </summary>
public record StructureDto(
    string InstitutionName, string? LogoUrl, string OrganizationType, string? CohortLabel, string? CohortLabelPlural,
    List<StructureAdminDto> Admins, List<StructureCohortDto> Cohorts, List<StructureCommunityDto> Communities,
    /// <summary>Members whose graduation year has no group yet (a sign the structure is incomplete).</summary>
    int MembersWithoutGroup,
    /// <summary>The graduation years behind <see cref="MembersWithoutGroup"/>, newest first, so the page can offer to create them in one go.</summary>
    List<int> YearsWithoutGroup,
    /// <summary>Scoped administrators with no group and no community: they can sign in but see nothing.</summary>
    List<string> AdminsWithoutScope,
    /// <summary>Groups and communities that no administrator is responsible for.</summary>
    List<string> UnmanagedCohortIds, List<string> UnmanagedCommunityIds);

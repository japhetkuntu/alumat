using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
namespace ReservEase.Alumni.Institution.Api.Engagement;

public record CohortsDto(int PeriodDays, IReadOnlyList<CohortRow> Cohorts, IReadOnlyList<AmbassadorRow> Ambassadors, int CohortsWithMembers, int CohortsCovered);

public record RecentMember(string Name, int Year, DateTime JoinedAt, bool ProfileIncomplete);

public record AmbassadorWorkspaceDto(
    string Name, IReadOnlyList<int> YearGroups, CohortsDto Cohorts, IReadOnlyList<RecentMember> RecentlyJoined,
    IReadOnlyList<RecommendationDto> Tasks);

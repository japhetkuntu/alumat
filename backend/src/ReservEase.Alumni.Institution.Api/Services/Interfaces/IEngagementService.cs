using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IEngagementService
{
    Task<IApiResponse<EngagementDashboardDto>> GetDashboardAsync(AuthData admin, int days, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true);

    /// <summary>Year groups and their ambassadors. A super administrator sees everything; an ambassador only their own year groups and themselves.</summary>
    Task<IApiResponse<MonthlyReportDto>> GetMonthlyReportAsync(AuthData admin, string? month, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true);
    Task<IApiResponse<CohortsDto>> GetCohortsAsync(AuthData admin, int days, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true);
    Task<IApiResponse<AmbassadorWorkspaceDto>> GetAmbassadorWorkspaceAsync(AuthData admin, int days, IReadOnlyCollection<string> disabledFeatures, bool usesYearGroups = true);
    /// <summary>An ambassador finishing or postponing a task delegated to them. Only tasks assigned to that person.</summary>
    Task<IApiResponse<RecommendationDto>> UpdateMyTaskAsync(AuthData admin, string id, string action);
    Task<IApiResponse<IReadOnlyList<HealthSnapshotDto>>> GetHealthHistoryAsync(int days, int limit);
    Task<IApiResponse<PgPagedResult<RecommendationDto>>> ListRecommendationsAsync(string? status, int page, int pageSize);
    Task<IApiResponse<RecommendationDto>> ResolveRecommendationAsync(AuthData admin, string id, string action, int? snoozeDays);
    Task<IApiResponse<RecommendationDto>> AssignRecommendationAsync(AuthData admin, string id, string? staffId);
    Task<IApiResponse<object>> SetChecklistItemAsync(AuthData admin, string itemKey, bool done);
}

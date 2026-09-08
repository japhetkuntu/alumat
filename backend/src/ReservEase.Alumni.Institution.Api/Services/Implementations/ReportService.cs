using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class ReportService(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    ILogger<ReportService> logger) : IReportService
{
    /// <summary>
    /// Member IDs belonging to one of a scoped admin's assigned communities
    /// (approved memberships only) — the join Members/Contributions need
    /// since neither carries a CommunityId of its own. Null for a SuperAdmin
    /// (unrestricted) or a ScopedAdmin with no communities assigned.
    /// </summary>
    private async Task<List<string>?> GetScopedCommunityMemberIdsAsync(AuthData admin)
    {
        if (admin.Role != StaffRoles.ScopedAdmin)
            return null;

        var communityIds = admin.CommunityIds ?? new List<string>();
        if (communityIds.Count == 0)
            return new List<string>();

        return (await membershipRepo.GetAllAsync(m => communityIds.Contains(m.CommunityId) && m.Status == "Approved"))
            .Select(m => m.MemberId)
            .Distinct()
            .ToList();
    }

    public async Task<IApiResponse<ReportSummaryDto>> GetReportSummaryAsync(AuthData admin)
    {
        try
        {
            logger.LogInformation("GetReportSummary request (admin: {AdminId}, role: {Role})", admin.Id, admin.Role);

            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            var communityIds = admin.CommunityIds ?? new List<string>();
            var communityMemberIds = await GetScopedCommunityMemberIdsAsync(admin);

            // Campaigns — year-scoped or community-scoped
            var campaignQuery = campaignRepo.GetQueryable(isSuper ? null : c =>
                c.CreatedBy == admin.Id
                || (c.YearGroups != null && c.YearGroups.Any(__y => yearGroups.Contains(__y)))
                || (c.CommunityId != null && communityIds.Contains(c.CommunityId)));
            var campaignStats = await campaignQuery
                .GroupBy(c => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(c => c.Status == CampaignStatus.Active),
                    Closed = g.Count(c => c.Status == CampaignStatus.Closed),
                })
                .FirstOrDefaultAsync();

            var totalCampaigns = campaignStats?.Total ?? 0;
            var activeCampaigns = campaignStats?.Active ?? 0;
            var closedCampaigns = campaignStats?.Closed ?? 0;

            // Members — batch (graduation year) or community membership
            var membersQuery = isSuper
                ? memberRepo.GetQueryable(null)
                : memberRepo.GetQueryable(m => yearGroups.Contains(m.GraduationYear) || communityMemberIds!.Contains(m.Id));
            var totalMembers = await membersQuery.CountAsync();

            // Contributions — via their Campaign's scope
            var campaignIdQuery = isSuper
                ? null
                : campaignRepo.GetQueryable(c =>
                    c.CreatedBy == admin.Id
                    || (c.YearGroups != null && c.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (c.CommunityId != null && communityIds.Contains(c.CommunityId))).Select(c => c.Id);

            var contributionQuery = isSuper
                ? contributionRepo.GetQueryable(null)
                : contributionRepo.GetQueryable(c => campaignIdQuery!.Contains(c.CampaignId));

            var contributionsStats = await contributionQuery
                .GroupBy(c => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Collected = g.Where(c => c.Status == "Successful").Sum(c => c.Amount)
                })
                .FirstOrDefaultAsync();

            var totalContributions = contributionsStats?.Count ?? 0;
            var totalCollected = contributionsStats?.Collected ?? 0;

            // Events — year-scoped or community-scoped
            var eventsQueryFiltered = isSuper
                ? eventRepo.GetQueryable(null)
                : eventRepo.GetQueryable(e =>
                    e.CreatedBy == admin.Id
                    || (e.YearGroups != null && e.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (e.CommunityId != null && communityIds.Contains(e.CommunityId)));
            var totalEvents = await eventsQueryFiltered.CountAsync();

            // Jobs — year-scoped or community-scoped
            var jobsQueryFiltered = isSuper
                ? jobRepo.GetQueryable(null)
                : jobRepo.GetQueryable(j =>
                    j.CreatedBy == admin.Id
                    || (j.YearGroups != null && j.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (j.CommunityId != null && communityIds.Contains(j.CommunityId)));
            var totalJobs = await jobsQueryFiltered.CountAsync();

            var summary = new ReportSummaryDto
            {
                TotalMembers = totalMembers,
                TotalContributions = totalContributions,
                TotalCollected = totalCollected,
                TotalCampaigns = totalCampaigns,
                ActiveCampaigns = activeCampaigns,
                ClosedCampaigns = closedCampaigns,
                TotalEvents = totalEvents,
                TotalJobs = totalJobs,
            };

            return summary.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get report summary");
            return ApiResponseExtensions.ToServerErrorApiResponse<ReportSummaryDto>("Failed to retrieve report summary");
        }
    }

    public async Task<IApiResponse<ReportExportResult>> ExportEntityCsvAsync(string entity, AuthData admin, MemberExportFilters? memberFilters = null)
    {
        try
        {
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            var communityIds = admin.CommunityIds ?? new List<string>();
            var communityMemberIds = await GetScopedCommunityMemberIdsAsync(admin);
            memberFilters ??= new MemberExportFilters(null, null, null, null, null);

            IQueryable<object>? source = entity.ToLower() switch
            {
                "campaigns" => campaignRepo.GetQueryable(isSuper ? null : c =>
                    c.CreatedBy == admin.Id
                    || (c.YearGroups != null && c.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (c.CommunityId != null && communityIds.Contains(c.CommunityId))),
                "members" => memberRepo.GetQueryable(m =>
                    (isSuper || yearGroups.Contains(m.GraduationYear) || communityMemberIds!.Contains(m.Id))
                    && (string.IsNullOrEmpty(memberFilters.Status) || m.Status == memberFilters.Status)
                    && (!memberFilters.GraduationYearFrom.HasValue || m.GraduationYear >= memberFilters.GraduationYearFrom.Value)
                    && (!memberFilters.GraduationYearTo.HasValue || m.GraduationYear <= memberFilters.GraduationYearTo.Value)
                    && (string.IsNullOrEmpty(memberFilters.JobTitleContains) || (m.JobTitle != null && m.JobTitle.ToLower().Contains(memberFilters.JobTitleContains.ToLower())))
                    && (string.IsNullOrEmpty(memberFilters.LocationContains) || (m.Location != null && m.Location.ToLower().Contains(memberFilters.LocationContains.ToLower())))),
                "contributions" => contributionRepo.GetQueryable(isSuper ? null : c => campaignRepo.GetQueryable(isSuper ? null : cc =>
                    cc.CreatedBy == admin.Id
                    || (cc.YearGroups != null && cc.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (cc.CommunityId != null && communityIds.Contains(cc.CommunityId))).Select(cc => cc.Id).Contains(c.CampaignId)),
                "events" => eventRepo.GetQueryable(isSuper ? null : e =>
                    e.CreatedBy == admin.Id
                    || (e.YearGroups != null && e.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (e.CommunityId != null && communityIds.Contains(e.CommunityId))),
                "jobs" => jobRepo.GetQueryable(isSuper ? null : j =>
                    j.CreatedBy == admin.Id
                    || (j.YearGroups != null && j.YearGroups.Any(__y => yearGroups.Contains(__y)))
                    || (j.CommunityId != null && communityIds.Contains(j.CommunityId))),
                _ => null,
            };

            if (source == null)
                return ApiResponseExtensions.ToBadRequestApiResponse<ReportExportResult>("Invalid export entity");

            var fileName = entity.ToLower() switch
            {
                "campaigns" => "campaigns-export.csv",
                "members" => "members-export.csv",
                "contributions" => "contributions-export.csv",
                "events" => "events-export.csv",
                "jobs" => "jobs-export.csv",
                _ => "export.csv",
            };

            var sb = new System.Text.StringBuilder();

            if (entity.ToLower() == "campaigns")
            {
                sb.AppendLine("Id,Title,Status,TargetAmount,CollectedAmount,PaidCount,YearGroups");
                foreach (var row in ((IQueryable<Campaign>)source).AsEnumerable())
                {
                    var years = row.YearGroups is null ? string.Empty : string.Join("|", row.YearGroups);
                    sb.AppendLine($"{row.Id},{EscapeCsv(row.Title)},{row.Status},{row.TargetAmount},{row.CollectedAmount},{row.PaidCount},{EscapeCsv(years)}");
                }
            }
            else if (entity.ToLower() == "members")
            {
                sb.AppendLine("Id,FirstName,LastName,Email,GraduationYear,DepartmentId,Status,JobTitle,Location");
                foreach (var row in ((IQueryable<Member>)source).AsEnumerable())
                {
                    sb.AppendLine($"{row.Id},{EscapeCsv(row.FirstName)},{EscapeCsv(row.LastName)},{row.Email},{row.GraduationYear},{EscapeCsv(row.DepartmentId)},{row.Status},{EscapeCsv(row.JobTitle ?? string.Empty)},{EscapeCsv(row.Location ?? string.Empty)}");
                }
            }
            else if (entity.ToLower() == "contributions")
            {
                sb.AppendLine("Id,CampaignId,MemberId,Amount,PaymentMethod,Status,ConfirmedAt,CreatedAt");
                foreach (var row in ((IQueryable<Contribution>)source).AsEnumerable())
                {
                    sb.AppendLine($"{row.Id},{row.CampaignId},{row.MemberId},{row.Amount},{row.PaymentMethod},{row.Status},{row.ConfirmedAt:o},{row.CreatedAt:o}");
                }
            }
            else if (entity.ToLower() == "events")
            {
                sb.AppendLine("Id,Title,StartDate,EndDate,Venue,Status,Capacity");
                foreach (var row in ((IQueryable<AlumniEvent>)source).AsEnumerable())
                {
                    sb.AppendLine($"{row.Id},{EscapeCsv(row.Title)},{row.StartDate:o},{row.EndDate:o},{EscapeCsv(row.Venue)},{row.Status},{row.Capacity}");
                }
            }
            else if (entity.ToLower() == "jobs")
            {
                sb.AppendLine("Id,Title,Company,Location,Type,Status,Deadline");
                foreach (var row in ((IQueryable<Job>)source).AsEnumerable())
                {
                    sb.AppendLine($"{row.Id},{EscapeCsv(row.Title)},{EscapeCsv(row.Company)},{EscapeCsv(row.Location)},{EscapeCsv(row.Type)},{row.Status},{row.Deadline:o}");
                }
            }

            var result = new ReportExportResult(fileName, sb.ToString());
            return result.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to export report entity {Entity}", entity);
            return ApiResponseExtensions.ToServerErrorApiResponse<ReportExportResult>("Failed to export report");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value?.Contains(',') == true || value?.Contains('"') == true || value?.Contains('\n') == true)
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value ?? string.Empty;
    }
}

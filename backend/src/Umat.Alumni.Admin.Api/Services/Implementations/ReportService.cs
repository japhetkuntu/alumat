using Microsoft.EntityFrameworkCore;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class ReportService(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<Job> jobRepo,
    ILogger<ReportService> logger) : IReportService
{
    public async Task<IApiResponse<ReportSummaryDto>> GetReportSummaryAsync(AuthData admin)
    {
        try
        {
            logger.LogInformation("GetReportSummary request (admin: {AdminId}, role: {Role})", admin.Id, admin.Role);

            var isSuper = string.Equals(admin.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
            var yearGroup = admin.GraduationYear;

            // Campaigns / year-scoped and global
            var campaignQuery = campaignRepo.GetQueryable(isSuper ? null : c => yearGroup.HasValue && c.YearGroups != null && c.YearGroups.Contains(yearGroup.Value));
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

            // Members
            var membersQuery = isSuper
                ? memberRepo.GetQueryable(null)
                : memberRepo.GetQueryable(m => yearGroup.HasValue && m.GraduationYear == yearGroup.Value);
            var totalMembers = await membersQuery.CountAsync();

            // Contributions
            var campaignIdQuery = isSuper
                ? null
                : campaignRepo.GetQueryable(c => yearGroup.HasValue && c.YearGroups != null && c.YearGroups.Contains(yearGroup.Value)).Select(c => c.Id);

            var contributionQuery = isSuper
                ? contributionRepo.GetQueryable(null)
                : contributionRepo.GetQueryable(c => campaignIdQuery!.Contains(c.CampaignId));

            var contributionsStats = await contributionQuery
                .GroupBy(c => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Collected = g.Where(c => c.Status == "Confirmed").Sum(c => c.Amount)
                })
                .FirstOrDefaultAsync();

            var totalContributions = contributionsStats?.Count ?? 0;
            var totalCollected = contributionsStats?.Collected ?? 0;

            // Events
            var eventsQueryFiltered = isSuper
                ? eventRepo.GetQueryable(null)
                : eventRepo.GetQueryable(e => yearGroup.HasValue && e.YearGroups != null && e.YearGroups.Contains(yearGroup.Value));
            var totalEvents = await eventsQueryFiltered.CountAsync();

            // Jobs
            var jobsQueryFiltered = isSuper
                ? jobRepo.GetQueryable(null)
                : jobRepo.GetQueryable(j => yearGroup.HasValue && j.YearGroups != null && j.YearGroups.Contains(yearGroup.Value));
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

    public async Task<IApiResponse<ReportExportResult>> ExportEntityCsvAsync(string entity, AuthData admin)
    {
        try
        {
            var isSuper = string.Equals(admin.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
            var yearGroup = admin.GraduationYear;

            IQueryable<object>? source = entity.ToLower() switch
            {
                "campaigns" => campaignRepo.GetQueryable(isSuper ? null : c => yearGroup.HasValue && c.YearGroups != null && c.YearGroups.Contains(yearGroup.Value)),
                "members" => memberRepo.GetQueryable(isSuper ? null : m => yearGroup.HasValue && m.GraduationYear == yearGroup.Value),
                "contributions" => contributionRepo.GetQueryable(isSuper ? null : c => campaignRepo.GetQueryable(isSuper ? null : cc => yearGroup.HasValue && cc.YearGroups != null && cc.YearGroups.Contains(yearGroup.Value)).Select(cc => cc.Id).Contains(c.CampaignId)),
                "events" => eventRepo.GetQueryable(isSuper ? null : e => yearGroup.HasValue && e.YearGroups != null && e.YearGroups.Contains(yearGroup.Value)),
                "jobs" => jobRepo.GetQueryable(isSuper ? null : j => yearGroup.HasValue && j.YearGroups != null && j.YearGroups.Contains(yearGroup.Value)),
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
                sb.AppendLine("Id,FirstName,LastName,Email,GraduationYear,DepartmentId,Status");
                foreach (var row in ((IQueryable<Member>)source).AsEnumerable())
                {
                    sb.AppendLine($"{row.Id},{EscapeCsv(row.FirstName)},{EscapeCsv(row.LastName)},{row.Email},{row.GraduationYear},{EscapeCsv(row.DepartmentId)},{row.Status}");
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

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public interface ITeamService
{
    Task<IApiResponse<TeamViewDto>> GetAsync();
    Task<IApiResponse<List<TeamAssigneeDto>>> GetAssigneesAsync();
    Task<IApiResponse<TeamMemberDto>> SetManagerAsync(string staffId, string? managerId, string actorId, string actorName);
    Task<IApiResponse<ReassignResult>> ReassignAsync(ReassignRequest request, string actorId, string actorName);
}

/// <summary>
/// The platform team's structure: SuperAdmins at the top, representatives under them, representatives under representatives.
/// A SuperAdmin sees all of it whoever it is assigned to. A representative sees themselves and the people beneath them.
/// </summary>
public class TeamService(
    IAlumniPgRepository<PlatformStaff> staffRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<OnboardingLead> leadRepo,
    IAuditLogService auditLog,
    IStaffScope staffScope,
    IHttpContextAccessor http,
    ILogger<TeamService> logger) : ITeamService
{
    private static readonly string[] InStructure = [PlatformStaffRoles.SuperAdmin, PlatformStaffRoles.Representative];

    private string MyId => http.HttpContext!.User.GetAccount().Id;

    public async Task<IApiResponse<TeamViewDto>> GetAsync()
    {
        try
        {
            var visibility = await staffScope.GetAsync();
            var all = (await staffRepo.GetAllAsync(_ => true)).ToList();
            var people = all.Where(s => InStructure.Contains(s.Role) && (visibility.Unrestricted || visibility.StaffIds.Contains(s.Id))).ToList();
            var ids = people.Select(p => p.Id).ToList();

            var institutions = (await institutionRepo.GetAllAsync(i => i.OwnerStaffId != null && ids.Contains(i.OwnerStaffId), ignoreQueryFilters: true)).ToList();
            var instIds = institutions.Select(i => i.Id).ToList();
            var memberCounts = (await memberRepo.GetQueryable(m => instIds.Contains(m.InstitutionId), ignoreQueryFilters: true)
                .GroupBy(m => m.InstitutionId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Id, x => x.Count);
            var leads = (await leadRepo.GetAllAsync(l => l.AssigneeStaffId != null && ids.Contains(l.AssigneeStaffId))).ToList();

            var directReports = all.Where(s => s.ReportsToStaffId != null).GroupBy(s => s.ReportsToStaffId!).ToDictionary(g => g.Key, g => g.Count());

            var rows = people.Select(p =>
            {
                var mine = institutions.Where(i => i.OwnerStaffId == p.Id).ToList();
                var myLeads = leads.Where(l => l.AssigneeStaffId == p.Id).ToList();
                return new TeamMemberDto(
                    p.Id, p.Name, p.Email, p.Role, p.ReportsToStaffId, p.IsDisabled, p.LastActiveAt,
                    directReports.GetValueOrDefault(p.Id), mine.Count, mine.Count(i => i.Status == "Active"),
                    mine.Sum(i => memberCounts.GetValueOrDefault(i.Id)),
                    myLeads.Count(l => l.Status != OnboardingLeadStatuses.Approved && l.Status != OnboardingLeadStatuses.Rejected),
                    myLeads.Count(l => l.Status == OnboardingLeadStatuses.Approved));
            }).OrderBy(r => r.Role == PlatformStaffRoles.SuperAdmin ? 0 : 1).ThenBy(r => r.Name).ToList();

            int? unassignedInstitutions = null, unassignedLeads = null;
            if (visibility.Unrestricted)
            {
                unassignedInstitutions = await institutionRepo.CountAsync(i => i.OwnerStaffId == null, ignoreQueryFilters: true);
                unassignedLeads = await leadRepo.CountAsync(l => l.AssigneeStaffId == null && l.Status != OnboardingLeadStatuses.Approved && l.Status != OnboardingLeadStatuses.Rejected);
            }

            var me = all.FirstOrDefault(s => s.Id == MyId);
            return new TeamViewDto(MyId, me?.Role ?? "", visibility.Unrestricted || people.Count > 1, rows, unassignedInstitutions, unassignedLeads).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading the team");
            return ApiResponseExtensions.ToServerErrorApiResponse<TeamViewDto>("Failed to load the team");
        }
    }

    public async Task<IApiResponse<List<TeamAssigneeDto>>> GetAssigneesAsync()
    {
        var visibility = await staffScope.GetAsync();
        var all = (await staffRepo.GetAllAsync(s => !s.IsDisabled)).ToList();
        return all.Where(s => InStructure.Contains(s.Role) && (visibility.Unrestricted || visibility.StaffIds.Contains(s.Id)))
            .OrderBy(s => s.Name).Select(s => new TeamAssigneeDto(s.Id, s.Name, s.Role)).ToList().ToOkApiResponse();
    }

    public async Task<IApiResponse<TeamMemberDto>> SetManagerAsync(string staffId, string? managerId, string actorId, string actorName)
    {
        try
        {
            var all = (await staffRepo.GetAllAsync(_ => true)).ToList();
            var person = all.FirstOrDefault(s => s.Id == staffId);
            if (person is null) return ApiResponseExtensions.ToNotFoundApiResponse<TeamMemberDto>("Team member not found");

            var problem = new StaffHierarchy(all).ValidateManager(person.Id, person.Role, string.IsNullOrWhiteSpace(managerId) ? null : managerId);
            if (problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<TeamMemberDto>(problem);

            person.ReportsToStaffId = string.IsNullOrWhiteSpace(managerId) ? null : managerId;
            person.UpdatedAt = DateTime.UtcNow;
            person.UpdatedBy = actorId;
            await staffRepo.UpdateAsync(person);
            await auditLog.LogAsync(actorId, actorName, "changed who someone reports to", $"{person.Name} → {all.FirstOrDefault(s => s.Id == managerId)?.Name ?? "no one"}");

            var view = (await GetAsync()).Data!.People.First(p => p.Id == staffId);
            return view.ToOkApiResponse("Reporting line updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error changing the reporting line for {StaffId}", staffId);
            return ApiResponseExtensions.ToServerErrorApiResponse<TeamMemberDto>("Failed to change the reporting line");
        }
    }

    public async Task<IApiResponse<ReassignResult>> ReassignAsync(ReassignRequest request, string actorId, string actorName)
    {
        try
        {
            var visibility = await staffScope.GetAsync();
            var all = (await staffRepo.GetAllAsync(_ => true)).ToList();
            var target = all.FirstOrDefault(s => s.Id == request.ToStaffId);
            if (target is null || target.IsDisabled || !InStructure.Contains(target.Role))
                return ApiResponseExtensions.ToBadRequestApiResponse<ReassignResult>("Choose an active SuperAdmin or representative to take this over.");
            // Nobody can hand work to someone outside their own team.
            if (!visibility.Unrestricted && !visibility.StaffIds.Contains(target.Id))
                return ApiResponseExtensions.ToBadRequestApiResponse<ReassignResult>("You can only hand work to someone in your own team.");

            var moved = new ReassignResult(0, 0);
            var institutions = 0; var leads = 0;
            if (request.InstitutionIds.Count > 0)
            {
                var items = (await institutionRepo.GetAllAsync(i => request.InstitutionIds.Contains(i.Id), ignoreQueryFilters: true)).ToList();
                foreach (var i in items.Where(i => visibility.CanSee(i.OwnerStaffId)))   // only what they could see to begin with
                {
                    i.OwnerStaffId = target.Id; i.UpdatedAt = DateTime.UtcNow; i.UpdatedBy = actorId;
                    await institutionRepo.UpdateAsync(i);
                    institutions++;
                }
            }
            if (request.LeadIds.Count > 0)
            {
                var items = (await leadRepo.GetAllAsync(l => request.LeadIds.Contains(l.Id))).ToList();
                foreach (var l in items.Where(l => visibility.CanSee(l.AssigneeStaffId)))
                {
                    l.AssigneeStaffId = target.Id; l.UpdatedAt = DateTime.UtcNow; l.UpdatedBy = actorId;
                    await leadRepo.UpdateAsync(l);
                    leads++;
                }
            }
            moved = new ReassignResult(institutions, leads);
            await auditLog.LogAsync(actorId, actorName, "reassigned institutions and leads", $"{institutions} institutions, {leads} leads → {target.Name}");
            return moved.ToOkApiResponse("Reassigned");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reassigning work");
            return ApiResponseExtensions.ToServerErrorApiResponse<ReassignResult>("Failed to reassign");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class OnboardingLeadService(AlumniDbContext db, IAuditLogService auditLog) : IOnboardingLeadService
{
    public async Task<IApiResponse<List<OnboardingLeadResponse>>> GetLeadsAsync(string? status)
    {
        var query = db.OnboardingLeads.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        var leads = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        var items = await ToDtosAsync(leads);
        return items.ToOkApiResponse();
    }

    public async Task<IApiResponse<OnboardingLeadResponse>> GetLeadByIdAsync(string id)
    {
        var lead = await db.OnboardingLeads.FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse();
    }

    public async Task<IApiResponse<OnboardingLeadResponse>> CreateAsync(CreateOnboardingLeadRequest request)
    {
        var lead = new OnboardingLead
        {
            InstitutionName = request.InstitutionName,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Country = request.Country,
            EstimatedMemberCount = request.EstimatedMemberCount,
            Message = request.Message,
        };
        db.OnboardingLeads.Add(lead);
        await db.SaveChangesAsync();

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToCreatedApiResponse();
    }

    public async Task<IApiResponse<OnboardingLeadResponse>> UpdateStatusAsync(string id, UpdateOnboardingLeadStatusRequest request, string actorId, string actorName)
    {
        var lead = await db.OnboardingLeads.FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        lead.Status = request.Status;
        if (!string.IsNullOrEmpty(request.ApprovedInstitutionId))
            lead.ApprovedInstitutionId = request.ApprovedInstitutionId;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = actorId;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, $"set onboarding lead status to {request.Status}", lead.InstitutionName);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse("Status updated");
    }

    public async Task<IApiResponse<OnboardingLeadResponse>> AddNoteAsync(string id, AddInternalNoteRequest request, string actorId, string actorName)
    {
        var lead = await db.OnboardingLeads.FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        lead.InternalNote = request.Note;
        lead.AssigneeStaffId ??= actorId;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = actorId;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, "added internal note", lead.InstitutionName);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse("Note added");
    }

    private async Task<List<OnboardingLeadResponse>> ToDtosAsync(List<OnboardingLead> leads)
    {
        var assigneeIds = leads.Where(l => l.AssigneeStaffId != null).Select(l => l.AssigneeStaffId!).Distinct().ToList();
        var assigneeNames = await db.PlatformStaff.Where(s => assigneeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var now = DateTime.UtcNow;
        return leads.Select(l => new OnboardingLeadResponse(
            l.Id, l.InstitutionName, l.ContactName, l.ContactEmail, l.ContactPhone,
            l.Country, l.EstimatedMemberCount, l.Message, l.Status,
            l.AssigneeStaffId, l.AssigneeStaffId != null && assigneeNames.TryGetValue(l.AssigneeStaffId, out var aName) ? aName : null,
            l.InternalNote, l.ApprovedInstitutionId,
            Math.Round((now - l.CreatedAt).TotalHours, 1))).ToList();
    }
}

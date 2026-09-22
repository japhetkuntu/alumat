using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class OnboardingLeadService(
    IAlumniPgRepository<OnboardingLead> onboardingLeadRepo,
    IAlumniPgRepository<PlatformStaff> platformStaffRepo,
    IAuditLogService auditLog,
    ILogger<OnboardingLeadService> logger) : IOnboardingLeadService
{
    public async Task<IApiResponse<List<OnboardingLeadResponse>>> GetLeadsAsync(string? status)
    {
        try
        {
        var query = onboardingLeadRepo.GetQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        var leads = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        var items = await ToDtosAsync(leads);
        return items.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetLeadsAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<OnboardingLeadResponse>>("Failed to getleads");
        }}

    public async Task<IApiResponse<OnboardingLeadResponse>> GetLeadByIdAsync(string id)
    {
        try
        {
        var lead = await onboardingLeadRepo.GetOneAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetLeadByIdAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<OnboardingLeadResponse>("Failed to getleadbyid");
        }}

    public async Task<IApiResponse<OnboardingLeadResponse>> CreateAsync(CreateOnboardingLeadRequest request)
    {
        try
        {
        var lead = new OnboardingLead
        {
            InstitutionName = request.InstitutionName,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Country = request.Country,
            EstimatedMemberCount = request.EstimatedMemberCount,
            OrganizationType = request.OrganizationType,
            ContactRole = request.ContactRole,
            PrimaryGoals = request.PrimaryGoals,
            CurrentMemberManagement = request.CurrentMemberManagement,
            DataImportStatus = request.DataImportStatus,
            PreferredContactChannel = request.PreferredContactChannel,
            PreferredContactTime = request.PreferredContactTime,
            TimeZone = request.TimeZone,
            Website = request.Website,
            Message = request.Message,
        };
        await onboardingLeadRepo.AddAsync(lead);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToCreatedApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "CreateAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<OnboardingLeadResponse>("Failed to create");
        }}

    public async Task<IApiResponse<OnboardingLeadResponse>> UpdateStatusAsync(string id, UpdateOnboardingLeadStatusRequest request, string actorId, string actorName)
    {
        try
        {
        var lead = await onboardingLeadRepo.GetOneAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        lead.Status = request.Status;
        if (!string.IsNullOrEmpty(request.ApprovedInstitutionId))
            lead.ApprovedInstitutionId = request.ApprovedInstitutionId;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = actorId;
        await onboardingLeadRepo.UpdateAsync(lead);

        await auditLog.LogAsync(actorId, actorName, $"set onboarding lead status to {request.Status}", lead.InstitutionName);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse("Status updated");

        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateStatusAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<OnboardingLeadResponse>("Failed to updatestatus");
        }}

    public async Task<IApiResponse<OnboardingLeadResponse>> AddNoteAsync(string id, AddInternalNoteRequest request, string actorId, string actorName)
    {
        try
        {
        var lead = await onboardingLeadRepo.GetOneAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        lead.InternalNote = request.Note;
        lead.AssigneeStaffId ??= actorId;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = actorId;
        await onboardingLeadRepo.UpdateAsync(lead);

        await auditLog.LogAsync(actorId, actorName, "added internal note", lead.InstitutionName);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse("Note added");

        }
        catch (Exception e)
        {
            logger.LogError(e, "AddNoteAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<OnboardingLeadResponse>("Failed to addnote");
        }}

    private async Task<List<OnboardingLeadResponse>> ToDtosAsync(List<OnboardingLead> leads)
    {
        var assigneeIds = leads.Where(l => l.AssigneeStaffId != null).Select(l => l.AssigneeStaffId!).Distinct().ToList();
        var assigneeNames = await platformStaffRepo.GetQueryable(s => assigneeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var now = DateTime.UtcNow;
        return leads.Select(l => new OnboardingLeadResponse(
            l.Id, l.InstitutionName, l.ContactName, l.ContactEmail, l.ContactPhone,
            l.Country, l.EstimatedMemberCount, l.OrganizationType, l.ContactRole, l.PrimaryGoals,
            l.CurrentMemberManagement, l.DataImportStatus, l.PreferredContactChannel,
            l.PreferredContactTime, l.TimeZone, l.Website, l.Message, l.Status,
            l.AssigneeStaffId, l.AssigneeStaffId != null && assigneeNames.TryGetValue(l.AssigneeStaffId, out var aName) ? aName : null,
            l.InternalNote, l.ApprovedInstitutionId,
            Math.Round((now - l.CreatedAt).TotalHours, 1))).ToList();
    }
}

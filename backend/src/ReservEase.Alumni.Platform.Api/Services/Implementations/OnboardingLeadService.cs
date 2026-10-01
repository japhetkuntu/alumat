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
    IAlumniPgRepository<Institution> institutionRepo,
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
            Source = "Website",
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

    public async Task<IApiResponse<OnboardingLeadResponse>> CreateByStaffAsync(CreateStaffOnboardingLeadRequest request, string actorId, string actorName)
    {
        try
        {
        var status = string.IsNullOrWhiteSpace(request.Status) ? OnboardingLeadStatuses.Contacted : request.Status;
        // Approved only happens through institution creation (which links ApprovedInstitutionId).
        if (!OnboardingLeadStatuses.All.Contains(status) || status == OnboardingLeadStatuses.Approved)
            return ApiResponseExtensions.ToBadRequestApiResponse<OnboardingLeadResponse>($"Unknown or unsupported status '{status}'");

        var now = DateTime.UtcNow;
        var lead = new OnboardingLead
        {
            InstitutionName = request.InstitutionName.Trim(),
            ContactName = request.ContactName.Trim(),
            ContactEmail = request.ContactEmail?.Trim() ?? string.Empty,
            ContactPhone = request.ContactPhone,
            ContactRole = request.ContactRole,
            OrganizationType = request.OrganizationType,
            EstimatedMemberCount = request.EstimatedMemberCount,
            Source = request.Source.Trim(),
            Status = status,
            InternalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            AssigneeStaffId = actorId,
            NextFollowUpAt = ToUtc(request.NextFollowUpAt),
            CreatedBy = actorId,
        };
        OnboardingLeadStatuses.StampStage(lead, status, now);
        await onboardingLeadRepo.AddAsync(lead);

        await auditLog.LogAsync(actorId, actorName, $"logged onboarding lead ({lead.Source})", lead.InstitutionName);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToCreatedApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "CreateByStaffAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<OnboardingLeadResponse>("Failed to log lead");
        }}

    public async Task<IApiResponse<OnboardingLeadResponse>> UpdateStatusAsync(string id, UpdateOnboardingLeadStatusRequest request, string actorId, string actorName)
    {
        try
        {
        var lead = await onboardingLeadRepo.GetOneAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        if (!OnboardingLeadStatuses.All.Contains(request.Status))
            return ApiResponseExtensions.ToBadRequestApiResponse<OnboardingLeadResponse>($"Unknown status '{request.Status}'");

        var previousStatus = lead.Status;
        lead.Status = request.Status;
        OnboardingLeadStatuses.StampStage(lead, request.Status, DateTime.UtcNow);
        if (!string.IsNullOrEmpty(request.ApprovedInstitutionId))
            lead.ApprovedInstitutionId = request.ApprovedInstitutionId;
        // Moving past the pipeline ends any pending follow-up.
        if (request.Status is OnboardingLeadStatuses.Approved or OnboardingLeadStatuses.Rejected)
            lead.NextFollowUpAt = null;

        // Trial → Approved converts the trial institution into a full one.
        if (request.Status == OnboardingLeadStatuses.Approved && previousStatus == OnboardingLeadStatuses.Trial
            && !string.IsNullOrEmpty(lead.ApprovedInstitutionId))
        {
            await institutionRepo.ExecuteUpdateAsync(i => i.Id == lead.ApprovedInstitutionId,
                s => s.SetProperty(i => i.TrialEndsAt, (DateTime?)null));
        }
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

    public async Task<IApiResponse<OnboardingLeadResponse>> UpdateAsync(string id, UpdateOnboardingLeadRequest request, string actorId, string actorName)
    {
        try
        {
        var lead = await onboardingLeadRepo.GetOneAsync(l => l.Id == id);
        if (lead is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<OnboardingLeadResponse>("Onboarding lead not found");

        if (!string.IsNullOrEmpty(request.AssigneeStaffId)
            && !await platformStaffRepo.GetQueryable(s => s.Id == request.AssigneeStaffId && !s.IsDisabled).AnyAsync())
            return ApiResponseExtensions.ToBadRequestApiResponse<OnboardingLeadResponse>("Assignee not found");

        var followUp = ToUtc(request.NextFollowUpAt);
        lead.InstitutionName = request.InstitutionName.Trim();
        lead.ContactName = request.ContactName.Trim();
        lead.ContactEmail = request.ContactEmail?.Trim() ?? string.Empty;
        lead.ContactPhone = Blank(request.ContactPhone);
        lead.ContactRole = Blank(request.ContactRole);
        lead.OrganizationType = Blank(request.OrganizationType);
        lead.EstimatedMemberCount = Blank(request.EstimatedMemberCount);
        lead.Source = Blank(request.Source) ?? lead.Source;
        lead.AssigneeStaffId = Blank(request.AssigneeStaffId);
        if (followUp != lead.NextFollowUpAt)
        {
            lead.NextFollowUpAt = followUp;
            lead.FollowUpReminderSentAt = null; // a new date earns a new reminder
        }
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = actorId;
        await onboardingLeadRepo.UpdateAsync(lead);

        await auditLog.LogAsync(actorId, actorName, "updated onboarding lead", lead.InstitutionName);

        var dto = (await ToDtosAsync([lead])).Single();
        return dto.ToOkApiResponse("Lead updated");

        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<OnboardingLeadResponse>("Failed to update lead");
        }}

    public async Task<IApiResponse<ImportOnboardingLeadsResponse>> ImportAsync(ImportOnboardingLeadsRequest request, string actorId, string actorName)
    {
        try
        {
        // Duplicates by institution name (case/spacing-insensitive) against existing leads and earlier rows in this file.
        static string Key(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        var existing = (await onboardingLeadRepo.GetQueryable().Select(l => l.InstitutionName).ToListAsync())
            .Select(Key).ToHashSet();

        var now = DateTime.UtcNow;
        var toAdd = new List<OnboardingLead>();
        var skipped = new List<ImportSkippedRow>();
        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowNumber = i + 1;
            var name = row.InstitutionName?.Trim() ?? string.Empty;
            if (name.Length == 0) { skipped.Add(new(rowNumber, name, "Missing institution name")); continue; }
            if (string.IsNullOrWhiteSpace(row.ContactName)) { skipped.Add(new(rowNumber, name, "Missing contact name")); continue; }
            var status = string.IsNullOrWhiteSpace(row.Status) ? OnboardingLeadStatuses.New : row.Status.Trim();
            if (!OnboardingLeadStatuses.All.Contains(status) || status == OnboardingLeadStatuses.Approved)
            { skipped.Add(new(rowNumber, name, $"Unknown stage '{row.Status}'")); continue; }
            if (!existing.Add(Key(name))) { skipped.Add(new(rowNumber, name, "Already in the pipeline")); continue; }

            var lead = new OnboardingLead
            {
                InstitutionName = name,
                ContactName = row.ContactName.Trim(),
                ContactEmail = row.ContactEmail?.Trim() ?? string.Empty,
                ContactPhone = Blank(row.ContactPhone),
                ContactRole = Blank(row.ContactRole),
                OrganizationType = Blank(row.OrganizationType),
                EstimatedMemberCount = Blank(row.EstimatedMemberCount),
                Source = Blank(row.Source) ?? "Import",
                Status = status,
                InternalNote = Blank(row.Note),
                NextFollowUpAt = ToUtc(row.NextFollowUpAt),
                AssigneeStaffId = actorId,
                CreatedBy = actorId,
            };
            OnboardingLeadStatuses.StampStage(lead, status, now);
            toAdd.Add(lead);
        }

        if (toAdd.Count > 0) await onboardingLeadRepo.AddRangeAsync(toAdd);
        await auditLog.LogAsync(actorId, actorName, $"imported {toAdd.Count} onboarding leads ({skipped.Count} skipped)", "Onboarding leads");

        return new ImportOnboardingLeadsResponse(toAdd.Count, skipped).ToOkApiResponse($"Imported {toAdd.Count} leads");

        }
        catch (Exception e)
        {
            logger.LogError(e, "ImportAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<ImportOnboardingLeadsResponse>("Failed to import leads");
        }}

    public async Task<IApiResponse<List<LeadAssigneeResponse>>> GetAssigneesAsync()
    {
        try
        {
        var staff = await platformStaffRepo.GetQueryable(s => !s.IsDisabled && (s.Role == "SuperAdmin" || s.Role == "Sales"))
            .OrderBy(s => s.Name)
            .Select(s => new LeadAssigneeResponse(s.Id, s.Name, s.Role))
            .ToListAsync();
        return staff.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetAssigneesAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<LeadAssigneeResponse>>("Failed to load assignees");
        }}

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Follow-up dates arrive as calendar dates from the browser; store them as UTC midnight so "due today" is unambiguous.</summary>
    private static DateTime? ToUtc(DateTime? value) =>
        value is { } v ? DateTime.SpecifyKind(v.Date, DateTimeKind.Utc) : null;

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

        var institutionIds = leads.Where(l => l.ApprovedInstitutionId != null).Select(l => l.ApprovedInstitutionId!).Distinct().ToList();
        var trialEnds = await institutionRepo.GetQueryable(i => institutionIds.Contains(i.Id) && i.TrialEndsAt != null)
            .ToDictionaryAsync(i => i.Id, i => i.TrialEndsAt);

        var now = DateTime.UtcNow;
        return leads.Select(l => new OnboardingLeadResponse(
            l.Id, l.InstitutionName, l.ContactName, l.ContactEmail, l.ContactPhone,
            l.Country, l.EstimatedMemberCount, l.OrganizationType, l.ContactRole, l.PrimaryGoals,
            l.CurrentMemberManagement, l.DataImportStatus, l.PreferredContactChannel,
            l.PreferredContactTime, l.TimeZone, l.Website, l.Message, l.Status,
            l.AssigneeStaffId, l.AssigneeStaffId != null && assigneeNames.TryGetValue(l.AssigneeStaffId, out var aName) ? aName : null,
            l.InternalNote, l.ApprovedInstitutionId,
            Math.Round((now - l.CreatedAt).TotalHours, 1),
            l.AgreementVersion, l.AgreementAcceptedAt, l.AgreementAcceptedByName, l.AgreementAcceptedByTitle, l.AgreementAcceptedIp,
            l.Source, l.CreatedAt, l.ContactedAt, l.DemoBookedAt, l.TrialStartedAt, l.ApprovedAt,
            l.NextFollowUpAt, l.ApprovedInstitutionId != null ? trialEnds.GetValueOrDefault(l.ApprovedInstitutionId) : null, l.MarketingShareId, l.MarketingAttribution)).ToList();
    }
}

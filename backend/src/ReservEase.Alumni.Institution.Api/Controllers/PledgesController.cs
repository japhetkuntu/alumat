using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Pledges to a fundraiser, for the administrators who can see that fundraiser. Members never see other people's
/// pledges or any pledged total; this is the only place they are exposed.
/// </summary>
[Authorize(Roles = "SuperAdmin,ScopedAdmin")]
[RequireFeature(InstitutionFeatures.Contributions)]
public class PledgesController(
    IAlumniPgRepository<Pledge> pledgeRepo,
    IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution> institutionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    IConfiguration configuration,
    IInstitutionAuditLogService auditLog,
    ILogger<PledgesController> logger) : DefaultController
{
    [HttpGet("campaign/{campaignId}")]
    [SwaggerOperation(Summary = "Pledges to a fundraiser", Description = "Every pledge to the fundraiser, with how much is paid, plus collected / pledged / outstanding totals kept separate.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CampaignPledgesDto>))]
    public async Task<IActionResult> ForCampaign(string campaignId)
    {
        var admin = User.GetAccount();
        var campaign = await campaignRepo.GetByIdAsync(campaignId);
        if (campaign is null || campaign.IsMembershipCampaign || !admin.CanViewScopedItem(campaign.YearGroups, campaign.CommunityId, campaign.CreatedBy))
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Fundraiser not found").ToActionResult();

        var pledges = (await pledgeRepo.GetAllAsync(p => p.CampaignId == campaignId)).OrderBy(p => p.DueDate).ToList();
        var contributions = (await contributionRepo.GetAllAsync(c => c.CampaignId == campaignId && c.Status == "Successful")).ToList();

        var now = DateTime.UtcNow;
        var rows = pledges.Select(p =>
        {
            var progress = PledgeProgress.Compute(p, contributions.Where(c => c.MemberId == p.MemberId), now);
            return new AdminPledgeDto(p.Id, p.MemberId, p.MemberName, p.Amount, progress.Paid, progress.Outstanding, p.DueDate, progress.State, p.StatusNote, p.CreatedAt);
        }).ToList();

        // Kept as three separate figures on purpose: pledged money is a promise, not funds, and must never be added into "collected".
        var live = rows.Where(r => r.State is PledgeStates.Pledged or PledgeStates.PartPaid or PledgeStates.Overdue).ToList();
        var dto = new CampaignPledgesDto(
            campaign.CollectedAmount,
            live.Sum(r => r.Amount),
            live.Sum(r => r.Outstanding),
            rows.Count(r => r.State == PledgeStates.Overdue),
            rows);
        return dto.ToOkApiResponse().ToActionResult();
    }

    [HttpPost("{id}/remind")]
    [SwaggerOperation(Summary = "Send a reminder now", Description = "Sends the member an in-app notice and an email about their outstanding pledge.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Remind(string id)
    {
        var admin = User.GetAccount();
        var (pledge, campaign, error) = await LoadForAdminAsync(id, admin, modify: true);
        if (error is not null) return error;

        var contributions = await contributionRepo.GetAllAsync(c => c.CampaignId == pledge!.CampaignId && c.MemberId == pledge.MemberId);
        var progress = PledgeProgress.Compute(pledge!, contributions, DateTime.UtcNow);
        if (progress.State is PledgeStates.Fulfilled or PledgeStates.Cancelled or PledgeStates.WrittenOff)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This pledge doesn't need a reminder.").ToActionResult();

        var member = await memberRepo.GetByIdAsync(pledge.MemberId);
        if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found").ToActionResult();

        var stage = progress.State == PledgeStates.Overdue ? PledgeReminderMessages.Overdue : PledgeReminderMessages.Upcoming;
        var (title, body, actionLabel) = PledgeReminderMessages.Build(stage, campaign!.Title, progress.Outstanding, pledge.DueDate);
        var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        var portalUrl = MemberPortalLinks.UrlOrEmpty(currentTenant.InstitutionSlug, institution?.CustomDomain, configuration["MemberPortalBaseDomain"]);
        var actionUrl = string.IsNullOrEmpty(portalUrl) ? string.Empty : $"{portalUrl}/payment-campaign/{campaign.Id}";

        await notificationRepo.AddAsync(new Notification
        {
            RecipientId = member.Id, RecipientType = "Member", Title = title, Body = body, Type = "PledgeReminder",
            RelatedEntityId = campaign.Id, RelatedEntityType = "Campaign", ActionUrl = actionUrl, CreatedBy = admin.Id,
        });
        if (!string.IsNullOrWhiteSpace(member.Email))
        {
            await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(new SendEmailRequest
            {
                To = [new EmailContact { Email = member.Email, Name = member.FirstName }],
                TemplateId = "notification",
                TemplateVariables = new { first_name = member.FirstName, title, body, badge_label = "Pledge Reminder", action_url = actionUrl, action_label = actionLabel },
            }, $"pledge reminder to {member.Email}"), logger);
        }

        await auditLog.LogAsync(admin, "Pledge Reminder Sent", $"{pledge.MemberName} — {campaign.Title}");
        return new object().ToOkApiResponse("Reminder sent").ToActionResult();
    }

    [HttpPatch("{id}")]
    [SwaggerOperation(Summary = "Update a pledge", Description = "Extend the date, reduce the amount, cancel it, or write it off. Recorded in the audit log.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Update(string id, [FromBody] UpdatePledgeRequest request)
    {
        var admin = User.GetAccount();
        var (pledge, campaign, error) = await LoadForAdminAsync(id, admin, modify: true);
        if (error is not null) return error;

        var changes = new List<string>();
        if (request.DueDate.HasValue)
        {
            var due = DateTime.SpecifyKind(request.DueDate.Value.Date, DateTimeKind.Utc);
            if (due != pledge!.DueDate)
            {
                changes.Add($"date {pledge.DueDate:yyyy-MM-dd} → {due:yyyy-MM-dd}");
                pledge.DueDate = due;
                // A new date deserves fresh reminders.
                pledge.UpcomingReminderSentAt = pledge.DueReminderSentAt = pledge.OverdueReminderSentAt = null;
            }
        }
        if (request.Amount.HasValue)
        {
            if (request.Amount.Value <= 0 || request.Amount.Value > pledge!.Amount)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("An amount can only be reduced, and must be more than zero.").ToActionResult();
            if (request.Amount.Value != pledge.Amount)
            {
                changes.Add($"amount GHS {pledge.Amount:N2} → GHS {request.Amount.Value:N2}");
                pledge.Amount = decimal.Round(request.Amount.Value, 2);
            }
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (request.Status is not (PledgeStatuses.Cancelled or PledgeStatuses.WrittenOff or PledgeStatuses.Open))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Unknown status.").ToActionResult();
            if (request.Status != pledge!.Status)
            {
                changes.Add($"status {pledge.Status} → {request.Status}");
                pledge.Status = request.Status;
            }
        }
        if (changes.Count == 0) return new object().ToOkApiResponse("Nothing to change").ToActionResult();

        if (!string.IsNullOrWhiteSpace(request.Note)) pledge!.StatusNote = request.Note.Trim();
        pledge!.UpdatedAt = DateTime.UtcNow;
        pledge.UpdatedBy = admin.Id;
        await pledgeRepo.UpdateAsync(pledge);
        await auditLog.LogAsync(admin, "Pledge Updated", $"{pledge.MemberName} — {campaign!.Title}: {string.Join("; ", changes)}");
        return new object().ToOkApiResponse("Pledge updated").ToActionResult();
    }

    private async Task<(Pledge? Pledge, Campaign? Campaign, IActionResult? Error)> LoadForAdminAsync(string id, AuthData admin, bool modify)
    {
        var pledge = await pledgeRepo.GetByIdAsync(id);
        var campaign = pledge is null ? null : await campaignRepo.GetByIdAsync(pledge.CampaignId);
        if (pledge is null || campaign is null
            || !(modify ? admin.CanModifyScopedItem(campaign.YearGroups, campaign.CreatedBy, campaign.CommunityId)
                        : admin.CanViewScopedItem(campaign.YearGroups, campaign.CommunityId, campaign.CreatedBy)))
            return (null, null, ApiResponseExtensions.ToNotFoundApiResponse<object>("Pledge not found").ToActionResult());
        return (pledge, campaign, null);
    }
}

public record CampaignPledgesDto(decimal Collected, decimal PledgedTotal, decimal OutstandingTotal, int OverdueCount, List<AdminPledgeDto> Pledges);

public record AdminPledgeDto(
    string Id, string MemberId, string MemberName, decimal Amount, decimal Paid, decimal Outstanding,
    DateTime DueDate, string State, string? Note, DateTime CreatedAt);

public record UpdatePledgeRequest(DateTime? DueDate, decimal? Amount, string? Status, string? Note);

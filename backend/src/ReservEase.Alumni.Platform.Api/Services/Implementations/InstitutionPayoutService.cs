using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Approves or rejects an institution's own payout-setup submission (see
/// Institution.Api's InstitutionController.SubmitPayoutSetup) — mirrors
/// BatchPayoutService and InstitutionManagementService.UpdatePaymentsAsync's
/// Paystack subaccount sync exactly. Every query here uses IgnoreQueryFilters()
/// since this service is not scoped to one tenant.
/// </summary>
public class InstitutionPayoutService(
    IAlumniPgRepository<Institution> institutionRepo, IAuditLogService auditLog, IPaystackService paystackService,
    IAlumniPgRepository<StaffEntity> staffRepo, IAlumniPgRepository<Notification> notificationRepo,
    ILogger<InstitutionPayoutService> logger) : IInstitutionPayoutService
{
    public async Task<IApiResponse<List<PendingInstitutionPayoutItem>>> GetPendingAsync()
    {
        try
        {
        var pending = await institutionRepo.GetAllAsync(
            i => i.PayoutStatus == "Pending" && i.PendingPayoutChanges != null, ignoreQueryFilters: true);

        var items = pending
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new PendingInstitutionPayoutItem(
                i.Id, i.Name,
                i.PendingPayoutChanges!.SettlementBankName, i.PendingPayoutChanges.SettlementAccountNumber, i.PendingPayoutChanges.SettlementAccountName,
                i.UpdatedAt ?? i.CreatedAt))
            .ToList();

        return items.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetPendingAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<PendingInstitutionPayoutItem>>("Failed to getpending");
        }}

    public async Task<IApiResponse<object>> ApproveAsync(string institutionId, string approvedBy, string actorName)
    {
        try
        {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Institution not found");

        var pending = institution.PendingPayoutChanges;
        if (institution.PayoutStatus != "Pending" || pending is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This institution has no pending payout setup to approve");

        var subaccountRequest = new SubaccountRequest
        {
            BusinessName = institution.Name,
            SettlementBank = pending.SettlementBankCode,
            AccountNumber = pending.SettlementAccountNumber,
            // Zero-Deduction model — the real split is computed per-transaction from PlatformFeePercentage.
            PercentageCharge = 0,
        };

        var existingCode = institution.PaystackSubaccountCode;
        if (!string.IsNullOrWhiteSpace(existingCode))
        {
            var existing = await paystackService.FetchSubaccountAsync(existingCode);
            if (!existing.Status || existing.Data is null)
            {
                logger.LogWarning(
                    "Institution {InstitutionId}'s stored Paystack subaccount {SubaccountCode} is no longer valid ({Message}) — creating a new one instead of updating",
                    institution.Id, existingCode, existing.Message);
                existingCode = null;
            }
        }

        var subaccount = string.IsNullOrWhiteSpace(existingCode)
            ? await paystackService.CreateSubaccountAsync(subaccountRequest)
            : await paystackService.UpdateSubaccountAsync(existingCode, subaccountRequest);

        if (!subaccount.Status)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Paystack subaccount sync failed: {subaccount.Message}");

        institution.SettlementBankCode = pending.SettlementBankCode;
        institution.SettlementBankName = pending.SettlementBankName;
        institution.SettlementAccountNumber = pending.SettlementAccountNumber;
        institution.SettlementAccountName = pending.SettlementAccountName;
        if (!string.IsNullOrWhiteSpace(subaccount.Data?.SubaccountCode))
            institution.PaystackSubaccountCode = subaccount.Data.SubaccountCode;

        institution.PayoutStatus = "Approved";
        institution.PendingPayoutChanges = null;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = approvedBy;
        await institutionRepo.UpdateAsync(institution);
        await NotifyInstitutionAdminsAsync(institution, "Payout setup approved", "Your payout details were approved and are now active.", approvedBy);

        await auditLog.LogAsync(approvedBy, actorName, "approved institution payout setup", institution.Name);

        logger.LogInformation("Institution {InstitutionId} payout setup approved by {ApproverId}", institution.Id, approvedBy);
        return new object().ToOkApiResponse("Payout setup approved");

        }
        catch (Exception e)
        {
            logger.LogError(e, "ApproveAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to approve");
        }}

    public async Task<IApiResponse<object>> RejectAsync(string institutionId, RejectInstitutionPayoutRequest request, string rejectedBy, string actorName)
    {
        try
        {
        var institution = await institutionRepo.GetOneAsync(i => i.Id == institutionId, ignoreQueryFilters: true);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Institution not found");

        if (institution.PayoutStatus != "Pending" || institution.PendingPayoutChanges is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This institution has no pending payout setup to reject");

        institution.PayoutStatus = "Rejected";
        institution.PendingPayoutChanges = null;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = rejectedBy;
        await institutionRepo.UpdateAsync(institution);
        await NotifyInstitutionAdminsAsync(institution, "Payout setup needs changes", string.IsNullOrWhiteSpace(request.Notes)
            ? "Your payout details were rejected. Please review and resubmit them."
            : $"Your payout details were rejected: {request.Notes}", rejectedBy);

        await auditLog.LogAsync(rejectedBy, actorName, $"rejected institution payout setup{(string.IsNullOrWhiteSpace(request.Notes) ? "" : $": {request.Notes}")}", institution.Name);

        logger.LogInformation("Institution {InstitutionId} payout setup rejected by {RejecterId}", institution.Id, rejectedBy);
        return new object().ToOkApiResponse("Payout setup rejected");

        }
        catch (Exception e)
        {
            logger.LogError(e, "RejectAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reject");
        }}

        private async Task NotifyInstitutionAdminsAsync(Institution institution, string title, string body, string actorId)
        {
            var admins = await staffRepo.GetAllAsync(
                s => s.InstitutionId == institution.Id && !s.IsDisabled,
                ignoreQueryFilters: true);
            var notifications = admins.Select(admin => new Notification
            {
                InstitutionId = institution.Id,
                RecipientId = admin.Id,
                RecipientType = "Admin",
                Title = title,
                Body = body,
                Type = "PayoutSetupDecision",
                RelatedEntityId = institution.Id,
                RelatedEntityType = "Institution",
                ActionUrl = "/settings",
                CreatedBy = actorId,
            }).ToList();
            if (notifications.Count > 0)
                await notificationRepo.AddRangeAsync(notifications);
        }
}

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using GroupEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Community;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using NotificationEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Notification;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Approves or rejects a group's payout-setup submission (see Institution.Api's
/// BatchesController.SubmitPayoutSetup) — mirrors InstitutionManagementService.
/// UpdatePaymentsAsync's Paystack subaccount sync, and BusinessDirectoryService's
/// approve/reject-pending-edit shape. Every query here uses IgnoreQueryFilters()
/// since this service is not scoped to one tenant.
/// </summary>
public class GroupPayoutService(
    IAlumniPgRepository<GroupEntity> groupRepo, IAlumniPgRepository<Institution> institutionRepo,
    IAuditLogService auditLog, IPaystackService paystackService,
    IAlumniPgRepository<StaffEntity> staffRepo, IAlumniPgRepository<NotificationEntity> notificationRepo,
    ILogger<GroupPayoutService> logger) : IGroupPayoutService
{
    public async Task<IApiResponse<List<PendingGroupPayoutItem>>> GetPendingAsync()
    {
        try
        {
        var pending = (await groupRepo.GetAllAsync(
            b => b.PayoutStatus == "Pending" && b.PendingPayoutChanges != null, ignoreQueryFilters: true)).ToList();

        if (pending.Count == 0)
            return new List<PendingGroupPayoutItem>().ToOkApiResponse();

        var institutionIds = pending.Select(b => b.InstitutionId).Distinct().ToList();
        var institutionNames = await institutionRepo.GetQueryable(i => institutionIds.Contains(i.Id), ignoreQueryFilters: true)
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var items = pending
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => new PendingGroupPayoutItem(
                b.Id, b.Name,
                b.InstitutionId, institutionNames.GetValueOrDefault(b.InstitutionId, "Unknown institution"),
                b.PendingPayoutChanges!.UseInstitutionAccount,
                b.PendingPayoutChanges.SettlementBankName, b.PendingPayoutChanges.SettlementAccountNumber, b.PendingPayoutChanges.SettlementAccountName,
                b.UpdatedAt ?? b.CreatedAt))
            .ToList();

        return items.ToOkApiResponse();

        }
        catch (Exception e)
        {
            logger.LogError(e, "GetPendingAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<PendingGroupPayoutItem>>("Failed to getpending");
        }}

    public async Task<IApiResponse<object>> ApproveAsync(string communityId, string approvedBy, string actorName)
    {
        try
        {
        var group = await groupRepo.GetOneAsync(b => b.Id == communityId, ignoreQueryFilters: true);
        if (group is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Group not found");

        var pending = group.PendingPayoutChanges;
        if (group.PayoutStatus != "Pending" || pending is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This group has no pending payout setup to approve");

        var institution = await institutionRepo.GetOneAsync(i => i.Id == group.InstitutionId, ignoreQueryFilters: true);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Institution not found");

        if (pending.UseInstitutionAccount)
        {
            // Linking, not a fresh subaccount — this group simply settles
            // through the institution's own account at charge time (see
            // ContributionService/StoreOrderService's batch-aware lookup).
            group.UseInstitutionAccount = true;
            group.PaystackSubaccountCode = null;
            group.SettlementBankCode = null;
            group.SettlementBankName = null;
            group.SettlementAccountNumber = null;
            group.SettlementAccountName = null;
        }
        else
        {
            var subaccountRequest = new SubaccountRequest
            {
                BusinessName = $"{institution.Name} — {group.Name}",
                SettlementBank = pending.SettlementBankCode,
                AccountNumber = pending.SettlementAccountNumber,
                // Zero-Deduction model, same as institutions — the real split
                // is computed per-transaction from Institution.PlatformFeePercentage.
                PercentageCharge = 0,
            };

            var existingCode = group.PaystackSubaccountCode;
            if (!string.IsNullOrWhiteSpace(existingCode))
            {
                var existing = await paystackService.FetchSubaccountAsync(existingCode);
                if (!existing.Status || existing.Data is null)
                {
                    logger.LogWarning(
                        "Group {CommunityId}'s stored Paystack subaccount {SubaccountCode} is no longer valid ({Message}) — creating a new one instead of updating",
                        group.Id, existingCode, existing.Message);
                    existingCode = null;
                }
            }

            var subaccount = string.IsNullOrWhiteSpace(existingCode)
                ? await paystackService.CreateSubaccountAsync(subaccountRequest)
                : await paystackService.UpdateSubaccountAsync(existingCode, subaccountRequest);

            if (!subaccount.Status)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Paystack subaccount sync failed: {subaccount.Message}");

            group.UseInstitutionAccount = false;
            group.SettlementBankCode = pending.SettlementBankCode;
            group.SettlementBankName = pending.SettlementBankName;
            group.SettlementAccountNumber = pending.SettlementAccountNumber;
            group.SettlementAccountName = pending.SettlementAccountName;
            if (!string.IsNullOrWhiteSpace(subaccount.Data?.SubaccountCode))
                group.PaystackSubaccountCode = subaccount.Data.SubaccountCode;
        }

        group.PayoutStatus = "Approved";
        group.PendingPayoutChanges = null;
        group.UpdatedAt = DateTime.UtcNow;
        group.UpdatedBy = approvedBy;
        await groupRepo.UpdateAsync(group);
        await NotifyInstitutionAdminsAsync(group.InstitutionId, group.Id, "Group payout approved", $"Payout setup for {group.Name} was approved and is now active.", approvedBy);

        await auditLog.LogAsync(approvedBy, actorName, $"approved payout setup for group \"{group.Name}\"", institution.Name);

        logger.LogInformation("Group {CommunityId} payout setup approved by {ApproverId}", group.Id, approvedBy);
        return new object().ToOkApiResponse("Payout setup approved");

        }
        catch (Exception e)
        {
            logger.LogError(e, "ApproveAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to approve");
        }}

    public async Task<IApiResponse<object>> RejectAsync(string communityId, RejectGroupPayoutRequest request, string rejectedBy, string actorName)
    {
        try
        {
        var group = await groupRepo.GetOneAsync(b => b.Id == communityId, ignoreQueryFilters: true);
        if (group is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Group not found");

        if (group.PayoutStatus != "Pending" || group.PendingPayoutChanges is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This group has no pending payout setup to reject");

        group.PayoutStatus = "Rejected";
        group.PendingPayoutChanges = null;
        group.UpdatedAt = DateTime.UtcNow;
        group.UpdatedBy = rejectedBy;
        await groupRepo.UpdateAsync(group);

        var institution = await institutionRepo.GetOneAsync(i => i.Id == group.InstitutionId, ignoreQueryFilters: true);
        await NotifyInstitutionAdminsAsync(group.InstitutionId, group.Id, "Group payout needs changes",
            string.IsNullOrWhiteSpace(request.Notes) ? $"Payout setup for {group.Name} was rejected. Please review and resubmit it." : $"Payout setup for {group.Name} was rejected: {request.Notes}", rejectedBy);
        await auditLog.LogAsync(rejectedBy, actorName, $"rejected payout setup for group \"{group.Name}\"{(string.IsNullOrWhiteSpace(request.Notes) ? "" : $": {request.Notes}")}", institution?.Name ?? group.InstitutionId);

        logger.LogInformation("Group {CommunityId} payout setup rejected by {RejecterId}", group.Id, rejectedBy);
        return new object().ToOkApiResponse("Payout setup rejected");

        }
        catch (Exception e)
        {
            logger.LogError(e, "RejectAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reject");
        }}

        private async Task NotifyInstitutionAdminsAsync(string institutionId, string communityId, string title, string body, string actorId)
        {
            var admins = await staffRepo.GetAllAsync(s => s.InstitutionId == institutionId && !s.IsDisabled, ignoreQueryFilters: true);
            var notifications = admins.Select(admin => new NotificationEntity
            {
                InstitutionId = institutionId,
                RecipientId = admin.Id,
                RecipientType = "Admin",
                Title = title,
                Body = body,
                Type = "PayoutSetupDecision",
                RelatedEntityId = communityId,
                RelatedEntityType = "Community",
                ActionUrl = "/communities",
                CreatedBy = actorId,
            }).ToList();
            if (notifications.Count > 0)
                await notificationRepo.AddRangeAsync(notifications);
        }
}

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using BatchEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Batch;
using BatchPendingChanges = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.BatchPayoutPendingChanges;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Approves or rejects a batch's payout-setup submission (see Institution.Api's
/// BatchesController.SubmitPayoutSetup) — mirrors InstitutionManagementService.
/// UpdatePaymentsAsync's Paystack subaccount sync, and BusinessDirectoryService's
/// approve/reject-pending-edit shape. Every query here uses IgnoreQueryFilters()
/// since this service is not scoped to one tenant.
/// </summary>
public class BatchPayoutService(
    AlumniDbContext db, IAuditLogService auditLog, IPaystackService paystackService,
    ILogger<BatchPayoutService> logger) : IBatchPayoutService
{
    public async Task<IApiResponse<List<PendingBatchPayoutItem>>> GetPendingAsync()
    {
        var pending = await db.Batches.IgnoreQueryFilters()
            .Where(b => b.PayoutStatus == "Pending" && b.PendingPayoutChanges != null)
            .ToListAsync();

        if (pending.Count == 0)
            return new List<PendingBatchPayoutItem>().ToOkApiResponse();

        var institutionIds = pending.Select(b => b.InstitutionId).Distinct().ToList();
        var institutionNames = await db.Institutions.IgnoreQueryFilters()
            .Where(i => institutionIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var items = pending
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => new PendingBatchPayoutItem(
                b.Id, b.Name, b.Year,
                b.InstitutionId, institutionNames.GetValueOrDefault(b.InstitutionId, "Unknown institution"),
                b.PendingPayoutChanges!.UseInstitutionAccount,
                b.PendingPayoutChanges.SettlementBankName, b.PendingPayoutChanges.SettlementAccountNumber, b.PendingPayoutChanges.SettlementAccountName,
                b.UpdatedAt ?? b.CreatedAt))
            .ToList();

        return items.ToOkApiResponse();
    }

    public async Task<IApiResponse<object>> ApproveAsync(string batchId, string approvedBy, string actorName)
    {
        var batch = await db.Batches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Batch not found");

        var pending = batch.PendingPayoutChanges;
        if (batch.PayoutStatus != "Pending" || pending is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This batch has no pending payout setup to approve");

        var institution = await db.Institutions.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == batch.InstitutionId);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Institution not found");

        if (pending.UseInstitutionAccount)
        {
            // Linking, not a fresh subaccount — this batch simply settles
            // through the institution's own account at charge time (see
            // ContributionService/StoreOrderService's batch-aware lookup).
            batch.UseInstitutionAccount = true;
            batch.PaystackSubaccountCode = null;
            batch.SettlementBankCode = null;
            batch.SettlementBankName = null;
            batch.SettlementAccountNumber = null;
            batch.SettlementAccountName = null;
        }
        else
        {
            var subaccountRequest = new SubaccountRequest
            {
                BusinessName = $"{institution.Name} — {batch.Name}",
                SettlementBank = pending.SettlementBankCode,
                AccountNumber = pending.SettlementAccountNumber,
                // Zero-Deduction model, same as institutions — the real split
                // is computed per-transaction from Institution.PlatformFeePercentage.
                PercentageCharge = 0,
            };

            var existingCode = batch.PaystackSubaccountCode;
            if (!string.IsNullOrWhiteSpace(existingCode))
            {
                var existing = await paystackService.FetchSubaccountAsync(existingCode);
                if (!existing.Status || existing.Data is null)
                {
                    logger.LogWarning(
                        "Batch {BatchId}'s stored Paystack subaccount {SubaccountCode} is no longer valid ({Message}) — creating a new one instead of updating",
                        batch.Id, existingCode, existing.Message);
                    existingCode = null;
                }
            }

            var subaccount = string.IsNullOrWhiteSpace(existingCode)
                ? await paystackService.CreateSubaccountAsync(subaccountRequest)
                : await paystackService.UpdateSubaccountAsync(existingCode, subaccountRequest);

            if (!subaccount.Status)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Paystack subaccount sync failed: {subaccount.Message}");

            batch.UseInstitutionAccount = false;
            batch.SettlementBankCode = pending.SettlementBankCode;
            batch.SettlementBankName = pending.SettlementBankName;
            batch.SettlementAccountNumber = pending.SettlementAccountNumber;
            batch.SettlementAccountName = pending.SettlementAccountName;
            if (!string.IsNullOrWhiteSpace(subaccount.Data?.SubaccountCode))
                batch.PaystackSubaccountCode = subaccount.Data.SubaccountCode;
        }

        batch.PayoutStatus = "Approved";
        batch.PendingPayoutChanges = null;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = approvedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(approvedBy, actorName, $"approved payout setup for batch \"{batch.Name}\"", institution.Name);

        logger.LogInformation("Batch {BatchId} payout setup approved by {ApproverId}", batch.Id, approvedBy);
        return new object().ToOkApiResponse("Payout setup approved");
    }

    public async Task<IApiResponse<object>> RejectAsync(string batchId, RejectBatchPayoutRequest request, string rejectedBy, string actorName)
    {
        var batch = await db.Batches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Batch not found");

        if (batch.PayoutStatus != "Pending" || batch.PendingPayoutChanges is null)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This batch has no pending payout setup to reject");

        batch.PayoutStatus = "Rejected";
        batch.PendingPayoutChanges = null;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = rejectedBy;
        await db.SaveChangesAsync();

        var institution = await db.Institutions.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == batch.InstitutionId);
        await auditLog.LogAsync(rejectedBy, actorName, $"rejected payout setup for batch \"{batch.Name}\"{(string.IsNullOrWhiteSpace(request.Notes) ? "" : $": {request.Notes}")}", institution?.Name ?? batch.InstitutionId);

        logger.LogInformation("Batch {BatchId} payout setup rejected by {RejecterId}", batch.Id, rejectedBy);
        return new object().ToOkApiResponse("Payout setup rejected");
    }
}

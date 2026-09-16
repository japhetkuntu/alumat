using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Temporalio.Workflows;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Contributions;

/// <summary>
/// Paystack's webhook always calls one fixed platform-wide URL (there is no
/// per-institution routing), so every activity here reads/writes tenant-agnostically
/// (IgnoreQueryFilters on the read side) and the correct institution is derived from
/// the data itself (the campaign the payment belongs to) rather than any ambient
/// tenant context — same principle the pre-Temporal ContributionService method this
/// replaces documented for itself.
/// </summary>
[Workflow("ProcessContributionCallback")]
public class ProcessContributionCallbackWorkflow : IProcessContributionCallbackWorkflow
{
    [WorkflowRun]
    public async Task<PaymentCallbackResult> RunAsync(ProcessPaymentCallbackRequest request)
    {
        var reference = request.Reference;
        Workflow.Logger.LogInformation(
            "[ContributionCallback] Processing {Provider} callback for reference {Reference}",
            request.Provider, reference);

        var transaction = await Workflow.ExecuteActivityAsync(
            (ContributionCallbackActivities a) => a.LoadTransactionAsync(reference),
            PaymentActivityOptions.DatabaseRead);

        if (transaction is null)
        {
            // We only ever call Paystack's InitializePaymentAsync after our own
            // PaymentTransaction row is durably committed, so a callback for a
            // reference with no matching row can't be our own data loss — it's a
            // wrong or bogus reference. Reject rather than reconstructing one.
            Workflow.Logger.LogError("No PaymentTransaction found for reference {Reference}. Rejecting callback as an unknown reference.", reference);
            return PaymentCallbackResult.BadRequest("Unknown payment reference.");
        }

        if (!string.IsNullOrEmpty(request.RawBody))
        {
            transaction.CallbackPayload = request.RawBody;
        }

        if (transaction.Status == "Successful")
        {
            if (!string.IsNullOrEmpty(request.RawBody))
                await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.SaveTransactionAsync(transaction), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment already verified and recorded");
        }

        var verify = await Workflow.ExecuteActivityAsync(
            (ContributionCallbackActivities a) => a.VerifyPaystackPaymentAsync(reference),
            PaymentActivityOptions.ExternalGateway);

        if (!verify.Status)
        {
            var friendly = GetFriendlyPaymentFailureMessage(verify.Message);
            transaction.Status = "Failed";
            transaction.FailureMessage = friendly;
            transaction.ProcessedAt = Workflow.UtcNow;
            await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.SaveTransactionAsync(transaction), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.BadRequest(friendly);
        }

        var paystackStatus = verify.PaystackStatus;
        var verifiedGrossAmount = verify.GrossAmount;

        // transaction.Amount already holds the school-intended amount set at
        // initiation — never overwrite it with Paystack's gross (grossed-up)
        // figure, or the institution's ledger would show the wrong number.
        transaction.GrossChargeAmount = verifiedGrossAmount;
        var actualGatewayFee = verify.GatewayFee ?? transaction.GatewayFeeAmount;

        var expectedGross = transaction.Amount + transaction.PlatformFeeAmount + actualGatewayFee;
        if (Math.Abs(expectedGross - verifiedGrossAmount) > 0.02m)
        {
            Workflow.Logger.LogWarning(
                "Zero-Deduction reconciliation mismatch for {Reference}: expected gross {Expected}, Paystack reported {Actual}. Institution amount is unaffected.",
                reference, expectedGross, verifiedGrossAmount);
        }

        var actualPlatformNet = transaction.TransactionChargeAmount - actualGatewayFee;
        if (transaction.TransactionChargeAmount > 0 && actualPlatformNet < transaction.PlatformFeeAmount)
        {
            Workflow.Logger.LogWarning(
                "Zero-Deduction safety buffer insufficient for {Reference}: platform netted {ActualNet}, target was {TargetFee}. Consider raising PaystackConfig.GatewayFeeSafetyBufferSubunit.",
                reference, actualPlatformNet, transaction.PlatformFeeAmount);
        }

        transaction.GatewayFeeAmount = actualGatewayFee;

        transaction.GatewayResponse = verify.GatewayResponse;
        transaction.ProcessedAt = Workflow.UtcNow;

        if (!string.IsNullOrEmpty(request.RawBody))
        {
            try
            {
                var payload = JObject.Parse(request.RawBody);
                transaction.Currency ??= payload.SelectToken("data.currency")?.ToString();
                transaction.Channel ??= payload.SelectToken("data.authorization.channel")?.ToString();
            }
            catch
            {
                // best effort; ignore if parsing fails
            }
        }

        if (paystackStatus != "success")
        {
            transaction.Status = paystackStatus == "pending" ? "Pending" : "Failed";
            await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.SaveTransactionAsync(transaction), PaymentActivityOptions.DatabaseWrite);
            if (transaction.Status != "Pending")
                await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.ClearReferenceCacheAsync(reference), PaymentActivityOptions.DatabaseWrite);
            return PaymentCallbackResult.Ok("Payment status updated");
        }

        transaction.Status = "Successful";

        var existingContribution = await Workflow.ExecuteActivityAsync(
            (ContributionCallbackActivities a) => a.LoadContributionByReferenceAsync(reference, transaction.MemberId),
            PaymentActivityOptions.DatabaseRead);

        if (existingContribution is null)
        {
            var campaign = await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.LoadCampaignAsync(transaction.CampaignId), PaymentActivityOptions.DatabaseRead);

            if (campaign is not null && campaign.IsMembershipCampaign)
            {
                var priorMembership = await Workflow.ExecuteActivityAsync(
                    (ContributionCallbackActivities a) => a.LoadPriorSuccessfulMembershipContributionAsync(campaign.Id, transaction.MemberId),
                    PaymentActivityOptions.DatabaseRead);

                if (priorMembership is not null)
                {
                    transaction.Status = "Failed";
                    transaction.FailureMessage = "Membership campaign already paid.";
                    transaction.ProcessedAt = Workflow.UtcNow;
                    await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.SaveTransactionAsync(transaction), PaymentActivityOptions.DatabaseWrite);
                    await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.ClearReferenceCacheAsync(reference), PaymentActivityOptions.DatabaseWrite);
                    return PaymentCallbackResult.BadRequest("Membership campaign has already been paid.");
                }
            }

            var memberSnapshot = transaction.Member;
            if (memberSnapshot is null && !string.IsNullOrEmpty(transaction.MemberId))
            {
                var member = await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.LoadMemberAsync(transaction.MemberId), PaymentActivityOptions.DatabaseRead);
                if (member is not null)
                {
                    memberSnapshot = new MemberSnapshot
                    {
                        Id = member.Id,
                        FirstName = member.FirstName,
                        LastName = member.LastName,
                        Email = member.Email,
                        ProfilePictureUrl = member.ProfilePictureUrl,
                    };
                }
            }

            var contributionInstitutionId = campaign?.InstitutionId ?? transaction.InstitutionId;

            // Zero-Deduction model: the institution's Amount is never reduced by a
            // fee. PlatformFeeAmount/NetAmountToInstitution stay 0-deduction (0 and
            // == Amount respectively) so every institution-facing view shows the
            // full intended amount with nothing subtracted. Our actual revenue and
            // Paystack's fee are recorded separately on
            // PlatformRevenueAmount/GatewayFeeAmount/GrossChargeAmount, never
            // surfaced via ContributionDto.
            var contribution = new Contribution
            {
                InstitutionId = contributionInstitutionId,
                MemberId = transaction.MemberId,
                CampaignId = transaction.CampaignId,
                Member = memberSnapshot,
                Campaign = campaign is not null ? new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title } : null,
                Amount = transaction.Amount,
                PaymentMethod = "Paystack",
                TransactionRef = reference,
                Status = "Successful",
                ConfirmedAt = Workflow.UtcNow,
                ConfirmedBy = "Paystack",
                CreatedBy = transaction.MemberId,
                PlatformFeeAmount = 0m,
                NetAmountToInstitution = transaction.Amount,
                PlatformRevenueAmount = transaction.TransactionChargeAmount - transaction.GatewayFeeAmount,
                GatewayFeeAmount = transaction.GatewayFeeAmount,
                GrossChargeAmount = transaction.GrossChargeAmount,
                IsGuestPayment = transaction.IsGuestPayment,
                SharedByMemberId = transaction.SharedByMemberId,
                ShowOnWallOfSupport = transaction.ShowOnWallOfSupport,
            };

            contribution = await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.CreateContributionAsync(contribution), PaymentActivityOptions.DatabaseWrite);

            // Expression-tree lambdas below (needed so Workflow.ExecuteActivityAsync can
            // resolve the activity name via reflection) can't contain `?.` — compute the
            // fallback values as plain locals first.
            var confirmedMemberEmail = contribution.Member?.Email ?? string.Empty;
            var confirmedMemberFirstName = contribution.Member?.FirstName ?? string.Empty;
            var confirmedCampaignTitle = contribution.Campaign?.Title ?? "your contribution";
            await Workflow.ExecuteActivityAsync(
                (ContributionCallbackActivities a) => a.DispatchContributionConfirmedAsync(
                    contributionInstitutionId, contribution.MemberId, confirmedMemberEmail,
                    confirmedMemberFirstName, contribution.Amount,
                    confirmedCampaignTitle, contribution.Id),
                PaymentActivityOptions.Notification);

            if (transaction.SetupRecurringGiving && !string.IsNullOrEmpty(transaction.MemberId))
                await TrySetUpRecurringGivingAsync(transaction, contribution, verify.Authorization);

            if (campaign is not null)
            {
                await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.UpdateCampaignTotalsAsync(campaign.Id, contribution.Amount), PaymentActivityOptions.DatabaseWrite);

                if (campaign.IsMembershipCampaign && !string.IsNullOrEmpty(transaction.MemberId))
                    await ReevaluateMembershipAsync(campaign.InstitutionId, transaction.MemberId);
            }
        }

        await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.SaveTransactionAsync(transaction), PaymentActivityOptions.DatabaseWrite);
        await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.ClearReferenceCacheAsync(reference), PaymentActivityOptions.DatabaseWrite);

        Workflow.Logger.LogInformation("Paystack payment verified and contribution recorded for member {MemberId}", transaction.MemberId);
        return PaymentCallbackResult.Ok("Payment verified and contribution recorded");
    }

    /// <summary>
    /// Turns this just-confirmed, member-present charge into a standing monthly
    /// gift — only possible when Paystack reports the authorization as reusable
    /// (true for most cards, false for most mobile money and bank-transfer
    /// channels). Silently does nothing otherwise: the one-off contribution the
    /// member actually paid for already succeeded either way.
    /// </summary>
    private static async Task TrySetUpRecurringGivingAsync(PaymentTransaction transaction, Contribution contribution, PaystackAuthorization? authorization)
    {
        if (authorization is null || !authorization.Reusable || string.IsNullOrWhiteSpace(authorization.AuthorizationCode))
        {
            Workflow.Logger.LogInformation(
                "Recurring giving requested for contribution {ContributionId} but the charge channel isn't reusable — skipping recurring setup.",
                contribution.Id);
            return;
        }

        var existing = await Workflow.ExecuteActivityAsync(
            (ContributionCallbackActivities a) => a.LoadActiveRecurringGivingAsync(transaction.MemberId, transaction.CampaignId),
            PaymentActivityOptions.DatabaseRead);
        if (existing is not null)
        {
            Workflow.Logger.LogInformation("Member {MemberId} already has an active recurring gift to campaign {CampaignId} — not creating a duplicate.", transaction.MemberId, transaction.CampaignId);
            return;
        }

        var recurring = new RecurringContribution
        {
            InstitutionId = contribution.InstitutionId,
            MemberId = transaction.MemberId,
            Member = contribution.Member,
            CampaignId = transaction.CampaignId,
            Campaign = contribution.Campaign,
            Amount = contribution.Amount,
            Status = "Active",
            AuthorizationCode = authorization.AuthorizationCode,
            CardLast4 = authorization.Last4,
            CardType = authorization.CardType,
            CardBank = authorization.Bank,
            NextChargeDate = Workflow.UtcNow.AddMonths(1),
            LastChargeAt = Workflow.UtcNow,
            LastChargeStatus = "Successful",
            CreatedBy = transaction.MemberId,
        };

        await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.CreateRecurringGivingAsync(recurring), PaymentActivityOptions.DatabaseWrite);

        Workflow.Logger.LogInformation("Set up recurring monthly gift for member {MemberId} to campaign {CampaignId}, amount {Amount}", transaction.MemberId, transaction.CampaignId, contribution.Amount);
    }

    /// <summary>Re-evaluates full membership: active = paid ALL campaigns from grad
    /// year through current year. Auto-approves a still-Pending member whose
    /// membership payment just confirmed.</summary>
    private static async Task ReevaluateMembershipAsync(string institutionId, string memberId)
    {
        var memberToUpdate = await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.LoadMemberAsync(memberId), PaymentActivityOptions.DatabaseRead);
        if (memberToUpdate is null) return;

        var now = Workflow.UtcNow;
        var currentYear = now.Year;
        var gradYear = memberToUpdate.GraduationYear;

        var reeval = await Workflow.ExecuteActivityAsync(
            (ContributionCallbackActivities a) => a.LoadMembershipReevalDataAsync(institutionId, memberId, gradYear, currentYear),
            PaymentActivityOptions.DatabaseRead);

        var allPaid = reeval.RequiredCampaignIds.All(id => reeval.PaidCampaignIds.Contains(id));
        var isActive = MembershipActivityCalculator.ResolveActive(reeval.MembershipActivePolicy, memberToUpdate.Status, allPaid);

        memberToUpdate.IsMembershipActive = isActive;
        memberToUpdate.MembershipExpiry = allPaid
            ? new DateTime(currentYear, 12, 31, 23, 59, 59, DateTimeKind.Utc)
            : null;
        memberToUpdate.MembershipYearsPaid = reeval.RequiredCampaignIds.Count(id => reeval.PaidCampaignIds.Contains(id));
        memberToUpdate.LastMembershipPaidAt = now;

        // Auto-approve pending members who confirmed their membership payment
        if (memberToUpdate.Status == "Pending")
        {
            if (string.IsNullOrEmpty(memberToUpdate.MemberNumber))
            {
                memberToUpdate.MemberNumber = await Workflow.ExecuteActivityAsync(
                    (ContributionCallbackActivities a) => a.GetNextMemberNumberAsync(institutionId, memberToUpdate.GraduationYear),
                    PaymentActivityOptions.DatabaseRead);
            }
            memberToUpdate.Status = "Active";
            memberToUpdate.UpdatedAt = Workflow.UtcNow;
            memberToUpdate.UpdatedBy = "system";
            Workflow.Logger.LogInformation("Auto-approved pending member {MemberId} with number {MemberNumber} after membership payment", memberToUpdate.Id, memberToUpdate.MemberNumber);
        }

        await Workflow.ExecuteActivityAsync((ContributionCallbackActivities a) => a.UpdateMemberMembershipAsync(memberToUpdate), PaymentActivityOptions.DatabaseWrite);
    }

    private static string GetFriendlyPaymentFailureMessage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "We couldn't confirm your payment. Please try again or contact support.";

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Contains("invalid reference") || normalized.Contains("could not find"))
            return "We couldn't find that payment reference. Please try again.";

        if (normalized.Contains("already verified") || normalized.Contains("already been verified"))
            return "This payment has already been processed.";

        if (normalized.Contains("insufficient funds") || normalized.Contains("card declined"))
            return "Your card was declined. Please check with your bank or try another payment method.";

        if (normalized.Contains("expired") || normalized.Contains("expired card"))
            return "Your payment method has expired. Please use a different card.";

        if (normalized.Contains("not authorised") || normalized.Contains("authorization"))
            return "The payment was not authorized. Please try again or use another payment method.";

        return "We couldn't confirm your payment. Please try again or contact support.";
    }
}

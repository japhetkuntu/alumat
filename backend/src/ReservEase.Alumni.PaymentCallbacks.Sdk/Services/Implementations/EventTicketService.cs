using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;

/// <summary>
/// Event ticket checkout. Same Zero-Deduction fee model as store checkout: the institution receives the full ticket
/// price and the platform's share is added to what the payer is charged. The EventRsvp row is both the pending and the
/// confirmed record, one per member per event, so a retried payment reuses it.
/// </summary>
public class EventTicketService(
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    ICurrentTenantService currentTenant,
    IPaystackService paystackService,
    PaystackConfig paystackConfig,
    ITemporalClientProvider temporalProvider,
    IConfiguration configuration,
    ILogger<EventTicketService> logger,
    IAlumniPgRepository<Community>? communityRepo = null) : IEventTicketService
{
    private readonly string _paystackCallbackUrl = BuildCallbackUrl(configuration);

    private static string BuildCallbackUrl(IConfiguration configuration)
    {
        var callbackUrl = configuration["PaystackConfig:CallbackUrl"] ?? string.Empty;
        if (!string.IsNullOrEmpty(callbackUrl) && !callbackUrl.EndsWith("/callback", StringComparison.OrdinalIgnoreCase))
            callbackUrl = callbackUrl.TrimEnd('/') + "/callback";
        return callbackUrl;
    }

    public async Task<IApiResponse<EventTicketCheckoutResponse>> InitiateAsync(EventTicketRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("Event ticket request for event {EventId} by member {MemberId}", request.EventId, member.Id);

            var ev = await eventRepo.GetByIdAsync(request.EventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<EventTicketCheckoutResponse>("Event not found");

            if (!string.IsNullOrEmpty(ev.CommunityId))
            {
                var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == ev.CommunityId && m.MemberId == member.Id);
                if (membership is not { Status: "Approved" })
                    return ApiResponseExtensions.ToForbiddenApiResponse<EventTicketCheckoutResponse>("You must be an approved member of this community to buy a ticket");
            }

            var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            var unavailable = EventTicketRules.WhyTicketsUnavailable(ev, institution?.PaidEventsEnabled == true);
            if (unavailable is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<EventTicketCheckoutResponse>(unavailable);

            var price = ev.TicketPrice!.Value;
            var existing = await rsvpRepo.GetOneAsync(r => r.EventId == ev.Id && r.MemberId == member.Id);

            // A payment the member started earlier may have gone through without the page coming back; settle it first.
            if (existing is { Status: EventRsvpStatuses.PendingPayment, TransactionRef: { Length: > 0 } pendingRef })
            {
                await SettleAsync(pendingRef);
                existing = await rsvpRepo.GetOneAsync(r => r.EventId == ev.Id && r.MemberId == member.Id);
            }
            if (existing is { Status: EventRsvpStatuses.Confirmed })
                return ApiResponseExtensions.ToConflictApiResponse<EventTicketCheckoutResponse>("You have already RSVPed to this event");

            if (ev.Capacity.HasValue)
            {
                // Seats held by people who are paying right now count too, so two people can't both take the last one.
                var cutoff = DateTime.UtcNow - EventTicketRules.SeatHoldWindow;
                var taken = await rsvpRepo.CountAsync(r => r.EventId == ev.Id && r.MemberId != member.Id
                    && (r.Status == EventRsvpStatuses.Confirmed
                        || (r.Status == EventRsvpStatuses.PendingPayment && (r.UpdatedAt ?? r.CreatedAt) > cutoff)));
                if (taken >= ev.Capacity.Value)
                    return ApiResponseExtensions.ToConflictApiResponse<EventTicketCheckoutResponse>(
                        "This event is full right now. If someone's payment doesn't go through a seat may free up, so try again in a few minutes.");
            }

            var charge = ZeroDeductionCharge.Build(paystackConfig, price, institution);
            var reference = PaystackReferencePrefix.NewReference(PaystackReferencePrefix.EventTicket);

            // Persist the pending ticket BEFORE telling Paystack, so any callback for this reference has a matching row.
            if (existing is null)
            {
                existing = new EventRsvp
                {
                    EventId = ev.Id,
                    Event = new EventSnapshot { Id = ev.Id, Title = ev.Title, Venue = ev.Venue },
                    MemberId = member.Id,
                    Member = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl },
                    CreatedBy = member.Id,
                };
                Apply(existing, price, reference, charge);
                await rsvpRepo.AddAsync(existing);
            }
            else
            {
                Apply(existing, price, reference, charge);
                existing.UpdatedAt = DateTime.UtcNow;
                await rsvpRepo.UpdateAsync(existing);
            }

            var response = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Reference = reference,
                Email = member.Email,
                Amount = charge.AmountSubunit,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string> { { "memberId", member.Id }, { "eventTicket", "true" }, { "eventId", ev.Id } },
                // A group's event pays into the group's own account once it has an approved one, otherwise the institution's.
                Subaccount = await SettlementAccounts.ForAudienceAsync(null, communityRepo, currentTenant.InstitutionId, null, ev.CommunityId)
                    ?? institution?.PaystackSubaccountCode,
                TransactionCharge = charge.TransactionChargeSubunit,
                Bearer = charge.Bearer,
            });

            if (!response.Status)
            {
                existing.Status = EventRsvpStatuses.PaymentFailed;
                existing.FailureMessage = response.Message;
                existing.UpdatedAt = DateTime.UtcNow;
                await rsvpRepo.UpdateAsync(existing);
                return ApiResponseExtensions.ToBadRequestApiResponse<EventTicketCheckoutResponse>(response.Message);
            }

            return new EventTicketCheckoutResponse { AuthorizationUrl = response.Data?.AuthorizationUrl, Reference = reference }
                .ToOkApiResponse("Payment started");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error starting ticket payment for event {EventId}, member {MemberId}", request.EventId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EventTicketCheckoutResponse>("Failed to start the payment");
        }
    }

    private static void Apply(EventRsvp rsvp, decimal price, string reference, ZeroDeductionCharge charge)
    {
        rsvp.Status = EventRsvpStatuses.PendingPayment;
        rsvp.AmountPaid = price;
        rsvp.TransactionRef = reference;
        rsvp.PaidAt = null;
        rsvp.FailureMessage = null;
        rsvp.PlatformFeeAmount = charge.PlatformFee;
        rsvp.GatewayFeeAmount = charge.GatewayFee;
        rsvp.TransactionChargeAmount = charge.TransactionChargeAmount;
        rsvp.GrossChargeAmount = charge.GrossCharge;
    }

    public Task<bool> OwnsReferenceAsync(string reference) =>
        rsvpRepo.GetOneAsync(r => r.TransactionRef == reference, ignoreQueryFilters: true).ContinueWith(t => t.Result is not null);

    public async Task<IApiResponse<EventTicketStatusResponse>> GetStatusAsync(string reference, AuthData member)
    {
        try
        {
            var rsvp = await rsvpRepo.GetOneAsync(r => r.TransactionRef == reference);
            if (rsvp is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<EventTicketStatusResponse>("RSVP not found");
            if (rsvp.MemberId != member.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<EventTicketStatusResponse>("Reference does not belong to the current member");

            if (rsvp.Status == EventRsvpStatuses.PendingPayment)
            {
                // Fallback in case the webhook hasn't landed yet.
                await SettleAsync(reference);
                rsvp = await rsvpRepo.GetOneAsync(r => r.TransactionRef == reference) ?? rsvp;
            }

            var status = rsvp.Status switch
            {
                EventRsvpStatuses.Confirmed => "Successful",
                EventRsvpStatuses.PendingPayment => "Pending",
                _ => "Failed",
            };
            return new EventTicketStatusResponse
            {
                Reference = reference,
                EventId = rsvp.EventId,
                EventTitle = rsvp.Event?.Title,
                Status = status,
                Amount = rsvp.AmountPaid,
                Message = status switch
                {
                    "Successful" => "Payment confirmed.",
                    "Pending" => "Payment has been started but not yet completed.",
                    _ => rsvp.FailureMessage ?? "Payment failed",
                },
            }.ToOkApiResponse("RSVP status retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving ticket status for reference {Reference}", reference);
            return ApiResponseExtensions.ToServerErrorApiResponse<EventTicketStatusResponse>("Failed to retrieve the RSVP status");
        }
    }

    private async Task SettleAsync(string reference)
    {
        if (!temporalProvider.IsAvailable) return;
        var handle = await temporalProvider.Client!.StartOrAttachAsync<IProcessEventTicketCallbackWorkflow, PaymentCallbackResult>(
            wf => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, null!)),
            $"payment-callback-eventticket-{reference}", OperationsTaskQueues.PaymentCallbackProcessing);
        await handle.GetResultAsync();
    }
}

using Microsoft.Extensions.Logging;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace ReservEase.Alumni.Operations.Worker.Workflows.EventTickets;

/// <summary>One activity, one I/O call; the workflow owns the sequencing and every decision.</summary>
public class EventTicketCallbackActivities(
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IPaystackService paystackService,
    ITemporalClientProvider temporalProvider,
    ILogger<EventTicketCallbackActivities> logger)
{
    [Activity("EventTicketCallback.LoadTicket")]
    public virtual Task<EventRsvp?> LoadTicketAsync(string reference) =>
        Wrap(() => rsvpRepo.GetOneAsync(r => r.TransactionRef == reference, ignoreQueryFilters: true), "load event ticket", reference);

    [Activity("EventTicketCallback.SaveTicket")]
    public virtual Task SaveTicketAsync(EventRsvp rsvp) =>
        Wrap(() => rsvpRepo.UpdateAsync(rsvp), "save event ticket", rsvp.Id);

    [Activity("EventTicketCallback.VerifyPaystackPayment")]
    public virtual Task<EventTicketPaystackVerifyResult> VerifyPaystackPaymentAsync(string reference) =>
        Wrap(async () =>
        {
            var response = await paystackService.VerifyPaymentAsync(reference);
            return new EventTicketPaystackVerifyResult
            {
                Status = response.Status,
                Message = response.Message,
                PaystackStatus = response.Data?.Status?.ToLowerInvariant() ?? "unknown",
                GrossAmount = (response.Data?.Amount ?? 0) / 100m,
                GatewayFee = response.Data?.Fees.HasValue == true ? response.Data!.Fees!.Value / 100m : null,
                GatewayResponse = response.Data?.GatewayResponse,
            };
        }, "verify Paystack payment", reference);

    /// <summary>Recounts the confirmed seats onto the event and returns the count (and the event title for the notification).</summary>
    [Activity("EventTicketCallback.SyncSeatCount")]
    public virtual Task<EventSeatSync> SyncSeatCountAsync(string eventId) =>
        Wrap(async () =>
        {
            var count = await rsvpRepo.CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatuses.Confirmed, ignoreQueryFilters: true);
            await eventRepo.ExecuteUpdateAsync(e => e.Id == eventId, s => s.SetProperty(e => e.RsvpCount, count), ignoreQueryFilters: true);
            var ev = await eventRepo.GetOneAsync(e => e.Id == eventId, ignoreQueryFilters: true);
            return new EventSeatSync { Count = count, Capacity = ev?.Capacity, EventTitle = ev?.Title };
        }, "sync event seat count", eventId);

    [Activity("EventTicketCallback.DispatchTicketConfirmed")]
    public virtual Task DispatchTicketConfirmedAsync(string institutionId, string memberId, string eventId, string eventTitle) =>
        Wrap(() => temporalProvider.EnqueueNotificationAsync(
            NotificationRequest.EventRsvpConfirmed(institutionId, memberId, eventId, eventTitle), logger),
            "dispatch ticket confirmed notification", eventId);

    private static async Task<T> Wrap<T>(Func<Task<T>> action, string what, string context)
    {
        try { return await action(); }
        catch (Exception ex) { throw new ApplicationFailureException($"Failed to {what} ({context})", ex); }
    }

    private static async Task Wrap(Func<Task> action, string what, string context)
    {
        try { await action(); }
        catch (Exception ex) { throw new ApplicationFailureException($"Failed to {what} ({context})", ex); }
    }
}

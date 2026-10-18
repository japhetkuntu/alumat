# Paid event tickets

An institution can charge for an event. The platform decides which institutions may.

## Rules (one place: `EventTicketRules` in `PostgresDb.Sdk/Entities/Alumni`)

- **Off by default.** `Institution.PaidEventsEnabled` is false for every institution, old and new. Platform SuperAdmin or Billing turns it on
  (`PATCH /institutions/{id}/paid-events`) at any time, like any other feature: it needs no events, no members and no payout account first.
  Creating or repricing a paid event does need the institution's payout account (`PaystackSubaccountCode`), checked in the organiser's form
  and on the server, so ticket money never lands in the platform account. Turning it off stops new paid events and new ticket sales; bought tickets stay valid.
- **Tickets are final.** No member cancel, no refund. So a paid event **cannot be cancelled** (the cancel action and editing the status are both
  refused) and **cannot be deleted** once a ticket is sold. No code path ever has to give money back.
- **The price is the organiser's to change, any time.** People who already bought keep what they paid (it is recorded on their ticket, and "collected"
  adds up what was actually paid); anyone buying afterwards pays the new price. Once a ticket is sold the event cannot become free. A free event
  that already has RSVPs cannot become paid.
- **Stopping sales.** An organiser can stop new tickets (and new free RSVPs on any event) with the event's registration switch (`PUT /events/{id}/registration?closed=true`) without cancelling. Tickets already bought stand, and a payment already started still completes. For a paid event this is the way to stop selling.
- **Seats.** Paid seats come only from a payment, never a free RSVP. A started payment holds its seat for 30 minutes
  (`SeatHoldWindow`). A ticket paid for after the event filled still stands and is logged as over capacity.

## How a ticket is bought

`EventTicketService` (PaymentCallbacks.Sdk), same shape as store checkout:

1. `POST /events/{id}/ticket` saves an `EventRsvp` as `PendingPayment` (one row per member per event, reused on retry), then starts Paystack with the
   zero-deduction charge (`ZeroDeductionCharge`). Reference prefix `ET_`.
2. Paystack's webhook (or the status poll) runs `ProcessEventTicketCallbackWorkflow`, which verifies the payment, sets the row `Confirmed`,
   recounts the event's seats and sends the RSVP-confirmed notification. A failed payment becomes `PaymentFailed`.
3. The member returns to `/events/ticket-status?reference=…`, which polls `GET /events/tickets/{reference}/status`.

Fees (`PlatformFeeAmount`, `GatewayFeeAmount`, …) live on the RSVP row for the platform's accounting and are never in an institution response.

## Platform accounting

Confirmed paid tickets count in: institution revenue, the dashboard total, payout forecasts, analytics totals and the payments list/detail
(source `EventTicket`). Pending and failed tickets show in the payments list with no earnings.

## Deploying

Run the `AddPaidEventTickets` migration first. Nothing else changes until the platform switches an institution on.

## Known limits

- A member who starts a second payment while an earlier one is still in flight replaces the earlier reference; a late success on the old
  reference is not matched to the ticket. The new payment settles the earlier one first, which makes this rare.
- Deleting a member account removes their tickets.

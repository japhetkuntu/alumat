# Paid event tickets

> **Wording.** Members and institutions see this as a paid **RSVP**, not a "ticket": "RSVP · GH₵150", "Paid RSVPs are final", "RSVP fee". The words ticket and ticketed remain only in code names (`EventTicketService`, `IsTicketed`, the `ET_` payment reference).


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

## Several RSVP prices

One paid event can offer more than one price, for example "Attendance only" and "With certificate". Up to six, each with a name, a price and an optional line saying what it includes.

- **Stored on the event.** `AlumniEvent.PriceOptions` is a small jsonb list. `TicketPrice` stays as the **lowest price on sale**, so lists can say "From GH₵100" and older code keeps working. An event with a price but no list (from before options existed) is read as a single option called "RSVP" with the id `standard` (`EventTicketRules.OptionsOf`), so nothing needed migrating.
- **Organiser.** The event form has a price editor: add another price, name it, switch a price off, remove one. A price people have bought cannot be removed (switch it off to stop selling it). Adding or repricing needs paid events switched on and a payout account, same as a single price. At least one price must stay on sale. Older clients that send only `ticketPrice` still work (it becomes one option). The RSVP list shows which price each person bought.
- **Member.** The event page lists the prices on sale as choices, each with its note. Nothing is pre-selected when there is a choice, and the RSVP button says "Choose an RSVP above" until they pick, so nobody pays for the wrong one. With a single price on sale no choice is needed. The confirm step and the confirmation page name the chosen price.
- **Checkout.** `EventTicketRequest.OptionId` picks the price; the server takes the amount from the saved option, never from the client. The ticket (`EventRsvp`) records `PriceOptionId`, `PriceOptionName` and `AmountPaid`, so renaming or repricing an option later never changes what someone paid. The payment verification is unchanged: it checks the charge stored on the ticket.
- **Everyone else.** Guests see "RSVP · From GH₵…" on a public event. Money collected, payouts and the payments list read `AmountPaid`, so they already add up across prices.

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

Run the `AddPaidEventTickets` migration first. Then `AddEventPriceOptions`, which adds the price list to events (default empty) and the option columns to RSVPs. Nothing else changes until the platform switches an institution on.

## Known limits

- A member who starts a second payment while an earlier one is still in flight replaces the earlier reference; a late success on the old
  reference is not matched to the ticket. The new payment settles the earlier one first, which makes this rare.
- Deleting a member account removes their tickets.

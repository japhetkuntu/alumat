namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// The rules for paid event tickets, in one place so the institution API, the member API and the tests agree.
/// A ticket is final: it cannot be cancelled by the member, and it is not refunded. The organiser cannot cancel a
/// paid event for the same reason, so no money ever has to be given back. The price, though, is the organiser's to change at any time.
/// </summary>
public static class EventTicketRules
{
    /// <summary>
    /// A ticket that has been paid for and settles to the institution in the given window: the same "confirmed in this window"
    /// test contributions, store orders and service requests use, so payout figures are computed the same way for every source.
    /// What the institution receives is <see cref="EventRsvp.AmountPaid"/>, the ticket price: the payer's extra fee is on top of it.
    /// </summary>
    public static System.Linq.Expressions.Expression<Func<EventRsvp, bool>> SettledBetween(DateTime start, DateTime end) =>
        t => t.Status == EventRsvpStatuses.Confirmed && t.AmountPaid > 0 && t.PaidAt != null && t.PaidAt >= start && t.PaidAt < end;

    /// <summary>The same rule for two windows at once, so a forecast reads both in one query.</summary>
    public static System.Linq.Expressions.Expression<Func<EventRsvp, bool>> SettledInEither(DateTime start1, DateTime end1, DateTime start2, DateTime end2) =>
        t => t.Status == EventRsvpStatuses.Confirmed && t.AmountPaid > 0 && t.PaidAt != null
            && ((t.PaidAt >= start1 && t.PaidAt < end1) || (t.PaidAt >= start2 && t.PaidAt < end2));

    public const decimal MaxTicketPrice = 100_000m;

    /// <summary>How long an unpaid ticket holds a seat while the member is paying.</summary>
    public static readonly TimeSpan SeatHoldWindow = TimeSpan.FromMinutes(30);

    public const string NoRefundNotice = "Paid RSVPs are final: they can't be cancelled or refunded.";

    public const string PaidEventsOffMessage =
        "Paid events aren't turned on for your community. Contact AlumUnion support to turn them on.";

    public const string PayoutMissingMessage =
        "Your community's payout account isn't set up yet, so the money has nowhere to go. Contact AlumUnion support to set it up first.";

    public const string CannotCancelMessage =
        "A paid event can't be cancelled, because paid RSVPs are not refunded. You can still change its date, venue and details; attendees are told.";

    public const string CannotDeleteMessage =
        "Some people have paid to RSVP to this event, so it can't be deleted. Paid RSVPs are not refunded.";

    public const string CannotBecomeFreeMessage =
        "People have already paid to RSVP, so this event can't become free. You can still change the price.";

    /// <summary>
    /// Checks the pricing part of a create or edit. <paramref name="existing"/> is null when creating.
    /// <paramref name="paidTicketsSold"/> counts confirmed paid tickets, and <paramref name="confirmedRsvps"/> every confirmed RSVP.
    /// Returns an error message to show the organiser, or null when the pricing is fine.
    /// </summary>
    public static string? ValidatePricing(
        bool isTicketed, decimal? ticketPrice, AlumniEvent? existing,
        bool paidEventsEnabled, bool hasPayoutAccount, int paidTicketsSold, int confirmedRsvps)
    {
        var wasTicketed = existing?.IsTicketed ?? false;

        if (!isTicketed)
        {
            // People have paid for seats, so the event stays a paid one. (The price itself is the organiser's to change at any time.)
            if (wasTicketed && paidTicketsSold > 0) return CannotBecomeFreeMessage;
            return null;
        }

        if (ticketPrice is not > 0)
            return "Enter the RSVP price.";
        if (ticketPrice > MaxTicketPrice)
            return $"The RSVP price can't be more than {MaxTicketPrice:N0}.";

        // The price can change whenever the organiser likes, even after tickets have sold: people who already bought keep what they paid
        // (it is recorded on their ticket), and anyone buying from then on pays the new price.
        var priceChanged = !wasTicketed || existing!.TicketPrice != ticketPrice;

        // Members who already RSVP'd for free would be sitting on a paid event without paying.
        if (!wasTicketed && confirmedRsvps > 0)
            return "People have already RSVP'd for free, so this event can't become a paid one. Create a new paid event instead.";

        // Anything that puts a price on the event needs the platform's go-ahead and a payout account.
        if (priceChanged)
        {
            if (!paidEventsEnabled) return PaidEventsOffMessage;
            if (!hasPayoutAccount) return PayoutMissingMessage;
        }
        return null;
    }

    /// <summary>A paid event cannot be cancelled, whether through the cancel action or by editing its status.</summary>
    public static bool CanCancel(AlumniEvent ev) => !ev.IsTicketed;

    /// <summary>Whether tickets can be bought right now: the event is paid, still open, and the platform allows it.</summary>
    public static string? WhyTicketsUnavailable(AlumniEvent ev, bool paidEventsEnabled)
    {
        if (!ev.IsTicketed || ev.TicketPrice is not > 0) return "This event doesn't sell tickets.";
        if (ev.Status is "Cancelled" or "Completed") return "This event is no longer open.";
        if (ev.RegistrationClosed) return "Ticket sales for this event are closed.";
        if (!paidEventsEnabled) return "Tickets for this event aren't on sale right now.";
        return null;
    }
}

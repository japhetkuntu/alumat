namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// The rules for paid event tickets, in one place so the institution API, the member API and the tests agree.
/// A ticket is final: it cannot be cancelled by the member, and it is not refunded. The organiser cannot cancel a
/// paid event for the same reason, so no money ever has to be given back.
/// </summary>
public static class EventTicketRules
{
    public const decimal MaxTicketPrice = 100_000m;

    /// <summary>How long an unpaid ticket holds a seat while the member is paying.</summary>
    public static readonly TimeSpan SeatHoldWindow = TimeSpan.FromMinutes(30);

    public const string NoRefundNotice = "Tickets are final: they can't be cancelled or refunded.";

    public const string PaidEventsOffMessage =
        "Paid events aren't turned on for your community. Contact AlumUnion support to turn them on.";

    public const string PayoutMissingMessage =
        "Your community's payout account isn't set up yet, so ticket money has nowhere to go. Contact AlumUnion support to set it up first.";

    public const string CannotCancelMessage =
        "A paid event can't be cancelled, because tickets are not refunded. You can still change its date, venue and details; attendees are told.";

    public const string CannotDeleteMessage =
        "This event has sold tickets, so it can't be deleted. Tickets are not refunded.";

    public const string PriceLockedMessage =
        "Tickets have already been sold, so the price can't change and the event can't become free.";

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
            if (wasTicketed && paidTicketsSold > 0) return PriceLockedMessage;
            return null;
        }

        if (ticketPrice is not > 0)
            return "Enter the ticket price.";
        if (ticketPrice > MaxTicketPrice)
            return $"The ticket price can't be more than {MaxTicketPrice:N0}.";

        var priceChanged = !wasTicketed || existing!.TicketPrice != ticketPrice;
        if (wasTicketed && paidTicketsSold > 0 && priceChanged)
            return PriceLockedMessage;

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
        if (!paidEventsEnabled) return "Tickets for this event aren't on sale right now.";
        return null;
    }
}

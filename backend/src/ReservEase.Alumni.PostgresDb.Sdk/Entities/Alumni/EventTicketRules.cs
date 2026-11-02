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
    public const int MaxPriceOptions = 6;
    public const int MaxOptionNameLength = 60;
    public const int MaxOptionDescriptionLength = 200;
    /// <summary>The id given to the one option of an event that was priced before options existed.</summary>
    public const string LegacyOptionId = "standard";

    /// <summary>
    /// The options an event is sold at. A single-price event from before options existed has no list but has a price, which counts as one
    /// option called "RSVP", so older events behave exactly as they did.
    /// </summary>
    public static IReadOnlyList<EventPriceOption> OptionsOf(AlumniEvent ev)
    {
        if (!ev.IsTicketed) return [];
        if (ev.PriceOptions is { Count: > 0 }) return ev.PriceOptions;
        return ev.TicketPrice is > 0 ? [new EventPriceOption { Id = LegacyOptionId, Name = "RSVP", Price = ev.TicketPrice.Value, IsActive = true }] : [];
    }

    /// <summary>The options a member can buy right now.</summary>
    public static IReadOnlyList<EventPriceOption> OnSale(AlumniEvent ev) => OptionsOf(ev).Where(o => o.IsActive && o.Price > 0).ToList();

    /// <summary>The lowest price on sale (or the lowest of all if none is), kept on <see cref="AlumniEvent.TicketPrice"/> for lists.</summary>
    public static decimal? LowestPrice(IReadOnlyList<EventPriceOption> options)
    {
        if (options.Count == 0) return null;
        var live = options.Where(o => o.IsActive).ToList();
        return (live.Count > 0 ? live : options.ToList()).Min(o => o.Price);
    }

    /// <summary>
    /// Turns what the organiser sent into the saved options, or says what is wrong. New options get an id; known ids are kept.
    /// <paramref name="existing"/> are the options the event has now and <paramref name="soldByOption"/> how many paid tickets each has,
    /// because an option people have bought cannot disappear (it can be switched off instead).
    /// </summary>
    public static string? CleanOptions(
        IReadOnlyList<EventPriceOptionInput>? input, IReadOnlyList<EventPriceOption> existing,
        IReadOnlyDictionary<string, int> soldByOption, out List<EventPriceOption> cleaned)
    {
        cleaned = [];
        if (input is null || input.Count == 0) return "Add at least one RSVP price.";
        if (input.Count > MaxPriceOptions) return $"An event can have up to {MaxPriceOptions} RSVP prices.";

        var known = existing.ToDictionary(o => o.Id);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in input)
        {
            var name = (o.Name ?? "").Trim();
            if (name.Length == 0) return "Give every RSVP price a name, for example “With certificate”.";
            if (name.Length > MaxOptionNameLength) return $"A price name can't be longer than {MaxOptionNameLength} characters.";
            if (!names.Add(name)) return $"Two prices are called “{name}”. Give each a different name.";
            var description = string.IsNullOrWhiteSpace(o.Description) ? null : o.Description.Trim();
            if (description is { Length: > MaxOptionDescriptionLength }) return $"The note under “{name}” can't be longer than {MaxOptionDescriptionLength} characters.";
            if (o.Price is not > 0) return $"Enter a price for “{name}”.";
            if (o.Price > MaxTicketPrice) return $"A price can't be more than {MaxTicketPrice:N0}.";

            var id = !string.IsNullOrWhiteSpace(o.Id) && known.ContainsKey(o.Id!) ? o.Id! : Guid.NewGuid().ToString("N");
            cleaned.Add(new EventPriceOption { Id = id, Name = name, Description = description, Price = Math.Round(o.Price.Value, 2), IsActive = o.IsActive });
        }
        if (!cleaned.Any(o => o.IsActive)) return "At least one RSVP price must be on sale.";

        var kept = cleaned.Select(o => o.Id).ToHashSet();
        foreach (var gone in existing.Where(o => !kept.Contains(o.Id)))
            if (soldByOption.TryGetValue(gone.Id, out var n) && n > 0)
                return $"“{gone.Name}” has been bought, so it can't be removed. Switch it off instead to stop selling it.";
        return null;
    }

    /// <summary>Whether adding or repricing happened, which is what needs the platform's go-ahead and a payout account.</summary>
    public static bool PricesChanged(IReadOnlyList<EventPriceOption> before, IReadOnlyList<EventPriceOption> after)
    {
        var old = before.ToDictionary(o => o.Id);
        foreach (var o in after)
            if (!old.TryGetValue(o.Id, out var b) || b.Price != o.Price || (o.IsActive && !b.IsActive)) return true;
        return false;
    }

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
        if (isTicketed && ticketPrice is not > 0) return "Enter the RSVP price.";
        if (isTicketed && ticketPrice > MaxTicketPrice) return $"The RSVP price can't be more than {MaxTicketPrice:N0}.";
        IReadOnlyList<EventPriceOption>? options = isTicketed ? [new EventPriceOption { Id = LegacyOptionId, Name = "RSVP", Price = ticketPrice!.Value }] : null;
        return ValidatePricing(isTicketed, options, existing, paidEventsEnabled, hasPayoutAccount, paidTicketsSold, confirmedRsvps);
    }

    /// <summary>
    /// The same checks for an event with several prices. <paramref name="options"/> are already cleaned by <see cref="CleanOptions"/>.
    /// </summary>
    public static string? ValidatePricing(
        bool isTicketed, IReadOnlyList<EventPriceOption>? options, AlumniEvent? existing,
        bool paidEventsEnabled, bool hasPayoutAccount, int paidTicketsSold, int confirmedRsvps)
    {
        var wasTicketed = existing?.IsTicketed ?? false;

        if (!isTicketed)
        {
            // People have paid for seats, so the event stays a paid one. (The price itself is the organiser's to change at any time.)
            if (wasTicketed && paidTicketsSold > 0) return CannotBecomeFreeMessage;
            return null;
        }

        if (options is not { Count: > 0 })
            return "Add at least one RSVP price.";

        // The price can change whenever the organiser likes, even after tickets have sold: people who already bought keep what they paid
        // (it is recorded on their ticket), and anyone buying from then on pays the new price.
        var priceChanged = !wasTicketed || PricesChanged(OptionsOf(existing!), options);

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
        if (!ev.IsTicketed || OptionsOf(ev).Count == 0) return "This event doesn't sell tickets.";
        if (OnSale(ev).Count == 0) return "RSVPs for this event aren't on sale right now.";
        if (ev.Status is "Cancelled" or "Completed") return "This event is no longer open.";
        if (ev.RegistrationClosed) return "Ticket sales for this event are closed.";
        if (!paidEventsEnabled) return "Tickets for this event aren't on sale right now.";
        return null;
    }
}

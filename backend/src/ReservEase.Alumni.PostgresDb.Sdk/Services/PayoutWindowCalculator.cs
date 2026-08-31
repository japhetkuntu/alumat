namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Paystack settles each institution's subaccount directly to its own bank
/// account at the start of every working day, covering everything confirmed
/// since the previous working-day cutoff. We have no Settlement API call or
/// webhook wired up, so this computes the same two windows both Institution.Api
/// and Platform.Api use to sum transactions — a pure, DB-free calculator so
/// the date math itself can never drift between the two APIs.
///
/// "Working day" is Monday–Friday only for now — no Ghana public-holiday
/// calendar. A bank holiday will show a payout as "expected" one day early.
/// Deliberate v1 scope cut, not an oversight.
///
/// Ghana (Africa/Accra) has no UTC offset and no DST, so all reasoning here
/// can operate directly on UTC dates — callers should pass DateTime.UtcNow.
/// </summary>
public static class PayoutWindowCalculator
{
    public static bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>The most recent working day on or before <paramref name="date"/> — today itself if today is Mon–Fri.</summary>
    public static DateOnly PreviousWorkingDayOnOrBefore(DateOnly date)
    {
        var d = date;
        while (!IsWorkingDay(d)) d = d.AddDays(-1);
        return d;
    }

    /// <summary>The next working day strictly after <paramref name="date"/>.</summary>
    public static DateOnly NextWorkingDayAfter(DateOnly date)
    {
        var d = date.AddDays(1);
        while (!IsWorkingDay(d)) d = d.AddDays(1);
        return d;
    }

    /// <summary>The working day strictly before <paramref name="date"/> (walks back past weekends).</summary>
    public static DateOnly PreviousWorkingDayBefore(DateOnly date)
    {
        var d = date.AddDays(-1);
        while (!IsWorkingDay(d)) d = d.AddDays(-1);
        return d;
    }

    public readonly record struct PayoutWindows(
        DateOnly LastPayoutDate, DateTime LastWindowStart, DateTime LastWindowEnd,
        DateOnly NextPayoutDate, DateTime NextWindowStart, DateTime NextWindowEnd);

    /// <summary>
    /// Given the current instant (UTC), returns the two settlement windows:
    /// the one that already paid out this/the most recent working morning
    /// (<see cref="PayoutWindows.LastPayoutDate"/>), and the one still
    /// accumulating toward the next working morning
    /// (<see cref="PayoutWindows.NextPayoutDate"/>).
    /// </summary>
    public static PayoutWindows GetWindows(DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);

        // The most recent morning a payout has already happened (today's, if
        // today is itself a working day — otherwise the last working day
        // before today, e.g. Friday all through the weekend).
        var lastPayoutDate = PreviousWorkingDayOnOrBefore(today);
        var lastWindowEnd = lastPayoutDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var lastWindowStart = PreviousWorkingDayBefore(lastPayoutDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Everything since that cutoff, up to now, pays out on the next working day.
        var nextPayoutDate = NextWorkingDayAfter(today);
        var nextWindowStart = lastWindowEnd;
        var nextWindowEnd = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        return new PayoutWindows(lastPayoutDate, lastWindowStart, lastWindowEnd, nextPayoutDate, nextWindowStart, nextWindowEnd);
    }
}

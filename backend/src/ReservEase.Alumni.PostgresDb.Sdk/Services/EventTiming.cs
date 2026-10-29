namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Whether an event is upcoming, on now or over, worked out from its dates. The stored Status is only ever set by an admin
/// (Upcoming when created, Cancelled when cancelled), and nothing moves an event on once its date passes, so reading Status
/// alone leaves finished events listed as Upcoming for ever.
/// </summary>
public static class EventTiming
{
    /// <summary>How long an event with no end date is taken to last.</summary>
    public static readonly TimeSpan DefaultLength = TimeSpan.FromHours(12);

    public static DateTime EndOf(DateTime start, DateTime? end) => end ?? start.Add(DefaultLength);

    /// <summary>Upcoming, Ongoing or Completed from the dates; a Cancelled (or already Completed) event stays as it is.</summary>
    public static string EffectiveStatus(string status, DateTime start, DateTime? end, DateTime now)
    {
        if (status is not ("Upcoming" or "Ongoing")) return status;
        if (EndOf(start, end) <= now) return "Completed";
        return start <= now ? "Ongoing" : "Upcoming";
    }
}

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One way to RSVP to a paid event, with its own price: for example "Attendance only" and "With certificate". Kept as a small list on the
/// event, because an event only ever has a handful and it is always read together with the event. Each RSVP records the option it bought
/// and the price it paid, so changing an option later never rewrites what someone already paid.
/// </summary>
public class EventPriceOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>A short line saying what this option includes, shown under its name.</summary>
    public string? Description { get; set; }
    public decimal Price { get; set; }
    /// <summary>Off stops new RSVPs for this option while people who bought it keep theirs.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>An option as the organiser sends it. A blank id is a new option.</summary>
public class EventPriceOptionInput
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public bool IsActive { get; set; } = true;
}

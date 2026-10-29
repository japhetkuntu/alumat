using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>An event's status follows its dates: nothing else ever moves an Upcoming event on once it has passed.</summary>
public class EventTimingTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_event_is_upcoming_before_it_starts_ongoing_while_it_runs_and_completed_after()
    {
        Assert.Equal("Upcoming", EventTiming.EffectiveStatus("Upcoming", Now.AddDays(3), Now.AddDays(3).AddHours(4), Now));
        Assert.Equal("Ongoing", EventTiming.EffectiveStatus("Upcoming", Now.AddHours(-1), Now.AddHours(3), Now));
        Assert.Equal("Completed", EventTiming.EffectiveStatus("Upcoming", Now.AddDays(-9), Now.AddDays(-9).AddHours(4), Now));
    }

    [Fact]
    public void An_event_with_no_end_date_is_taken_to_last_half_a_day()
    {
        Assert.Equal("Ongoing", EventTiming.EffectiveStatus("Upcoming", Now.AddHours(-11), null, Now));
        Assert.Equal("Completed", EventTiming.EffectiveStatus("Upcoming", Now.AddHours(-13), null, Now));
        Assert.Equal("Upcoming", EventTiming.EffectiveStatus("Upcoming", Now.AddHours(1), null, Now));
    }

    [Fact]
    public void A_cancelled_event_stays_cancelled_however_old_it_is()
    {
        Assert.Equal("Cancelled", EventTiming.EffectiveStatus("Cancelled", Now.AddDays(-30), null, Now));
        Assert.Equal("Cancelled", EventTiming.EffectiveStatus("Cancelled", Now.AddDays(30), null, Now));
    }
}

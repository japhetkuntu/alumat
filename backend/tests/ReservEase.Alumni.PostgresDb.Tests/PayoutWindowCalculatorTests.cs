using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class PayoutWindowCalculatorTests
{
    // 2026-10-05 is a Monday.
    private static DateOnly D(int day) => new(2026, 10, day);
    private static DateTime At(int day, int hour = 12) => new(2026, 10, day, hour, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(5, true)] [InlineData(6, true)] [InlineData(7, true)] [InlineData(8, true)] [InlineData(9, true)]
    [InlineData(10, false)] [InlineData(11, false)]
    public void Only_monday_to_friday_are_working_days(int day, bool expected)
        => Assert.Equal(expected, PayoutWindowCalculator.IsWorkingDay(D(day)));

    [Theory]
    [InlineData(5, 5)]   // Mon -> Mon
    [InlineData(9, 9)]   // Fri -> Fri
    [InlineData(10, 9)]  // Sat -> Fri
    [InlineData(11, 9)]  // Sun -> Fri
    public void PreviousWorkingDayOnOrBefore_stays_put_on_working_days_and_walks_back_over_weekends(int day, int expected)
        => Assert.Equal(D(expected), PayoutWindowCalculator.PreviousWorkingDayOnOrBefore(D(day)));

    [Theory]
    [InlineData(5, 6)]   // Mon -> Tue
    [InlineData(8, 9)]   // Thu -> Fri
    [InlineData(9, 12)]  // Fri -> Mon
    [InlineData(10, 12)] // Sat -> Mon
    [InlineData(11, 12)] // Sun -> Mon
    public void NextWorkingDayAfter_skips_weekends(int day, int expected)
        => Assert.Equal(D(expected), PayoutWindowCalculator.NextWorkingDayAfter(D(day)));

    [Theory]
    [InlineData(6, 5)]   // Tue -> Mon
    [InlineData(5, 2)]   // Mon -> previous Fri (Oct 2)
    [InlineData(10, 9)]  // Sat -> Fri
    [InlineData(11, 9)]  // Sun -> Fri
    [InlineData(12, 9)]  // Mon -> Fri
    public void PreviousWorkingDayBefore_is_strictly_earlier(int day, int expected)
        => Assert.Equal(expected == 2 ? new DateOnly(2026, 10, 2) : D(expected), PayoutWindowCalculator.PreviousWorkingDayBefore(D(day)));

    [Fact]
    public void Midweek_windows_cover_yesterday_to_today_and_today_to_now()
    {
        var w = PayoutWindowCalculator.GetWindows(At(7, 15)); // Wednesday 15:00

        Assert.Equal(D(7), w.LastPayoutDate);
        Assert.Equal(At(6, 0), w.LastWindowStart);
        Assert.Equal(At(7, 0), w.LastWindowEnd);
        Assert.Equal(D(8), w.NextPayoutDate);
        Assert.Equal(At(7, 0), w.NextWindowStart);
        Assert.Equal(At(7, 15), w.NextWindowEnd);
    }

    [Fact]
    public void Monday_last_window_reaches_back_across_the_weekend_to_friday()
    {
        var w = PayoutWindowCalculator.GetWindows(At(5, 9));

        Assert.Equal(D(5), w.LastPayoutDate);
        Assert.Equal(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), w.LastWindowStart);
        Assert.Equal(At(5, 0), w.LastWindowEnd);
        Assert.Equal(D(6), w.NextPayoutDate);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void On_a_weekend_the_last_payout_was_friday_and_the_next_is_monday(int day)
    {
        var w = PayoutWindowCalculator.GetWindows(At(day));

        Assert.Equal(D(9), w.LastPayoutDate);
        Assert.Equal(At(8, 0), w.LastWindowStart);
        Assert.Equal(At(9, 0), w.LastWindowEnd);
        Assert.Equal(D(12), w.NextPayoutDate);
        Assert.Equal(At(9, 0), w.NextWindowStart);
        Assert.Equal(At(day), w.NextWindowEnd);
    }

    [Fact]
    public void Friday_next_payout_is_the_following_monday()
        => Assert.Equal(D(12), PayoutWindowCalculator.GetWindows(At(9)).NextPayoutDate);

    [Fact]
    public void Windows_are_contiguous_and_marked_utc()
    {
        foreach (var day in Enumerable.Range(5, 14))
        {
            var w = PayoutWindowCalculator.GetWindows(new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc).AddDays(day - 1));
            Assert.Equal(w.LastWindowEnd, w.NextWindowStart);
            Assert.True(w.LastWindowStart < w.LastWindowEnd);
            Assert.True(w.NextWindowStart <= w.NextWindowEnd);
            Assert.Equal(DateTimeKind.Utc, w.LastWindowStart.Kind);
            Assert.Equal(DateTimeKind.Utc, w.NextWindowEnd.Kind);
            Assert.True(PayoutWindowCalculator.IsWorkingDay(w.LastPayoutDate));
            Assert.True(PayoutWindowCalculator.IsWorkingDay(w.NextPayoutDate));
        }
    }

    [Fact]
    public void An_unspecified_kind_instant_is_returned_as_utc()
    {
        var w = PayoutWindowCalculator.GetWindows(new DateTime(2026, 10, 7, 15, 0, 0, DateTimeKind.Unspecified));
        Assert.Equal(DateTimeKind.Utc, w.NextWindowEnd.Kind);
    }
}

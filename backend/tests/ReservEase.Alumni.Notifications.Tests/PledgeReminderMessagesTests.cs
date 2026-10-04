using ReservEase.Alumni.Notifications.Sdk.Models;

namespace ReservEase.Alumni.Notifications.Tests;

public class PledgeReminderMessagesTests
{
    private static readonly DateTime Due = new(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Stage_constants_are_stable_because_they_are_persisted()
    {
        Assert.Equal("Upcoming", PledgeReminderMessages.Upcoming);
        Assert.Equal("Due", PledgeReminderMessages.Due);
        Assert.Equal("Overdue", PledgeReminderMessages.Overdue);
    }

    [Fact]
    public void Upcoming_mentions_amount_campaign_and_planned_date()
    {
        var (title, body, action) = PledgeReminderMessages.Build(PledgeReminderMessages.Upcoming, "Library fund", 250m, Due);
        Assert.Equal("Your pledge is coming up", title);
        Assert.Contains("GHS 250.00", body);
        Assert.Contains("\"Library fund\"", body);
        Assert.Contains("November 5, 2026", body);
        Assert.Equal("Give now", action);
    }

    [Fact]
    public void Due_says_today_and_omits_the_date()
    {
        var (title, body, action) = PledgeReminderMessages.Build(PledgeReminderMessages.Due, "Library fund", 99.5m, Due);
        Assert.Equal("Today's the day for your pledge", title);
        Assert.Contains("GHS 99.50", body);
        Assert.DoesNotContain("November", body);
        Assert.Equal("Complete my pledge", action);
    }

    [Fact]
    public void Overdue_is_gentle_and_offers_cancelling()
    {
        var (title, body, action) = PledgeReminderMessages.Build(PledgeReminderMessages.Overdue, "Library fund", 10m, Due);
        Assert.Equal("A gentle reminder about your pledge", title);
        Assert.Contains("by November 5, 2026", body);
        Assert.Contains("cancel the pledge", body);
        Assert.Equal("Complete my pledge", action);
    }

    [Fact]
    public void Unknown_stage_falls_back_to_the_overdue_wording()
    {
        var unknown = PledgeReminderMessages.Build("Whatever", "C", 1m, Due);
        var overdue = PledgeReminderMessages.Build(PledgeReminderMessages.Overdue, "C", 1m, Due);
        Assert.Equal(overdue, unknown);
    }

    [Theory]
    [InlineData(1234567.891, "GHS 1,234,567.89")]
    [InlineData(0, "GHS 0.00")]
    [InlineData(0.005, "GHS 0.01")]
    public void Amount_uses_two_decimals_and_thousands_separators(double amount, string expected)
    {
        var (_, body, _) = PledgeReminderMessages.Build(PledgeReminderMessages.Due, "C", (decimal)amount, Due);
        Assert.Contains(expected, body);
    }
}

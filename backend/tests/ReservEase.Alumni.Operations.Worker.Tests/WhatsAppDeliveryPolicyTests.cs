using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Services;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public class WhatsAppDeliveryPolicyTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (WhatsAppDeliveryPolicy policy, Clock clock) Create(params (string, string)[] settings)
    {
        var clock = new Clock(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));   // mid-morning, UTC
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Item1, s => (string?)s.Item2)).Build();
        return (new WhatsAppDeliveryPolicy(new InMemoryRedisService<MemberRedisConfig>(), config, clock), clock);
    }

    [Fact]
    public async Task A_receipt_always_goes_even_in_quiet_hours_and_after_the_limit()
    {
        var (policy, clock) = Create(("WhatsAppAssistant:DailyLimit", "1"));
        clock.Now = clock.Now.Date.AddHours(2);   // 02:00 UTC

        for (var i = 0; i < 5; i++)
            Assert.True(await policy.TryReserveAsync("+233244000111", WhatsAppPriority.Transactional));
    }

    [Fact]
    public async Task Reminders_and_news_stop_at_the_daily_limit_for_one_number_but_not_for_another()
    {
        var (policy, _) = Create(("WhatsAppAssistant:DailyLimit", "2"));

        Assert.True(await policy.TryReserveAsync("0244000111", WhatsAppPriority.Reminder));
        Assert.True(await policy.TryReserveAsync("+233244000111", WhatsAppPriority.Update));   // the same number written another way
        Assert.False(await policy.TryReserveAsync("233244000111", WhatsAppPriority.Reminder));
        Assert.True(await policy.TryReserveAsync("0244000222", WhatsAppPriority.Reminder));
    }

    [Fact]
    public async Task The_limit_starts_again_the_next_day()
    {
        var (policy, clock) = Create(("WhatsAppAssistant:DailyLimit", "1"));
        Assert.True(await policy.TryReserveAsync("0244000111", WhatsAppPriority.Reminder));
        Assert.False(await policy.TryReserveAsync("0244000111", WhatsAppPriority.Reminder));

        clock.Now = clock.Now.AddDays(1);

        Assert.True(await policy.TryReserveAsync("0244000111", WhatsAppPriority.Reminder));
    }

    [Theory]
    [InlineData(22, false)]   // late evening
    [InlineData(3, false)]    // night
    [InlineData(6, true)]     // quiet hours end at 06:00
    [InlineData(12, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]   // and begin at 21:00
    public async Task Reminders_are_held_back_overnight_across_midnight(int hour, bool allowed)
    {
        var (policy, clock) = Create();
        clock.Now = clock.Now.Date.AddHours(hour);

        Assert.Equal(allowed, await policy.TryReserveAsync("0244000111", WhatsAppPriority.Reminder));
    }

    [Fact]
    public async Task A_limit_of_zero_holds_back_all_non_urgent_messages()
    {
        var (policy, _) = Create(("WhatsAppAssistant:DailyLimit", "0"));

        Assert.False(await policy.TryReserveAsync("0244000111", WhatsAppPriority.Update));
        Assert.True(await policy.TryReserveAsync("0244000111", WhatsAppPriority.Transactional));
    }
}

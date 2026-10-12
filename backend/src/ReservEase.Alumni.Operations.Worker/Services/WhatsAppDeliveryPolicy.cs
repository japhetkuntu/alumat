using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Operations.Worker.Services;

/// <summary>
/// The rules that keep WhatsApp welcome rather than noisy, and the shared number safe. A receipt or decision always goes. A
/// reminder or an update is held back during quiet hours and stops once a member has had their daily share, so a busy day for an
/// institution never turns into a burst of messages to one person.
///
/// Settings (all optional): WhatsAppAssistant:DailyLimit (default 3), WhatsAppAssistant:QuietFromHourUtc (default 21) and
/// WhatsAppAssistant:QuietToHourUtc (default 6). Hours are UTC, which is Ghana time. Counts live in Redis for a day and are
/// best-effort: a rare double count under a race only ever means one message fewer.
/// </summary>
public interface IWhatsAppDeliveryPolicy
{
    /// <summary>Whether a message of this priority may go to this number now. When it may, the daily count is taken.</summary>
    Task<bool> TryReserveAsync(string phone, string priority);
}

public class WhatsAppDeliveryPolicy(IRedisService<MemberRedisConfig> redis, IConfiguration configuration, TimeProvider? time = null) : IWhatsAppDeliveryPolicy
{
    private readonly TimeProvider clock = time ?? TimeProvider.System;

    public async Task<bool> TryReserveAsync(string phone, string priority)
    {
        if (priority == WhatsAppPriority.Transactional) return true;

        var now = clock.GetUtcNow();
        if (InQuietHours(now.Hour)) return false;

        var limit = configuration.GetValue("WhatsAppAssistant:DailyLimit", 3);
        if (limit <= 0) return false;

        // One person, one count, however the number was typed ("024…", "+233 24…", "233 24…").
        var number = PhoneNumbers.Normalize(phone) ?? new string(phone.Where(char.IsAsciiDigit).ToArray());
        var key = $"wa:daily:{now:yyyyMMdd}:{number}";
        var used = await redis.GetAsync<int>(key);
        if (used >= limit) return false;

        await redis.SetAsync(key, used + 1, TimeSpan.FromHours(26));
        return true;
    }

    private bool InQuietHours(int hour)
    {
        var from = configuration.GetValue("WhatsAppAssistant:QuietFromHourUtc", 21);
        var to = configuration.GetValue("WhatsAppAssistant:QuietToHourUtc", 6);
        if (from == to) return false;
        return from < to ? hour >= from && hour < to : hour >= from || hour < to;   // a window that wraps midnight
    }
}

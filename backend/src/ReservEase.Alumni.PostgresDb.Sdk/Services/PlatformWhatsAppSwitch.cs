using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Whether the platform administrator has the WhatsApp bot switched on (<see cref="PlatformSettings.WhatsAppBotEnabled"/>). Read by
/// every API and the worker, so one switch in the platform portal turns the channel off everywhere without a restart. The answer
/// is remembered for a few seconds per process to keep a busy webhook from reading the database on every message.
/// </summary>
public interface IPlatformWhatsAppSwitch
{
    Task<bool> IsOnAsync();
}

public class PlatformWhatsAppSwitch(IAlumniPgRepository<PlatformSettings> settingsRepo) : IPlatformWhatsAppSwitch
{
    private static readonly TimeSpan Remember = TimeSpan.FromSeconds(10);
    private static bool cachedValue = true;
    private static DateTime cachedUntil = DateTime.MinValue;

    /// <summary>Forgets the remembered answer, so a change made in this process shows at once.</summary>
    public static void Forget() => cachedUntil = DateTime.MinValue;

    public async Task<bool> IsOnAsync()
    {
        if (DateTime.UtcNow < cachedUntil) return cachedValue;
        try
        {
            var row = await settingsRepo.GetOneAsync(s => s.Id == PlatformSettings.SingletonId, ignoreQueryFilters: true);
            cachedValue = row?.WhatsAppBotEnabled ?? true;   // no row yet: nobody has switched it off
            cachedUntil = DateTime.UtcNow + Remember;
        }
        catch
        {
            // A database hiccup must not flip the channel; keep the last known answer a little longer.
            cachedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        }
        return cachedValue;
    }
}

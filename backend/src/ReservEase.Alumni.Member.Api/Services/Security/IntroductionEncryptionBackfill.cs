using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Security;

/// <summary>
/// Runs once when the API starts and encrypts any introduction text that was saved before encryption existed. It does nothing once
/// everything is converted, so it is safe to leave in and safe to run again after an interrupted start.
/// </summary>
public sealed class IntroductionEncryptionBackfill(IServiceScopeFactory scopes, IMessageProtector protector, ILogger<IntroductionEncryptionBackfill> logger) : BackgroundService
{
    private const int Batch = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Yield();   // let the host finish starting first
            using var scope = scopes.CreateScope();
            var intros = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<Introduction>>();
            var messages = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<IntroductionMessage>>();
            var converted = 0;

            // The same rows keep matching until they are converted, so each pass takes the next unconverted batch.
            while (!stoppingToken.IsCancellationRequested)
            {
                var rows = intros.GetQueryable(i => !i.Reason.StartsWith("v1.") || (i.LastMessagePreview != null && !i.LastMessagePreview.StartsWith("v1.")), ignoreQueryFilters: true).Take(Batch).ToList();
                if (rows.Count == 0) break;
                foreach (var i in rows)
                {
                    if (!protector.IsProtected(i.Reason)) i.Reason = protector.Protect(i.Reason, $"intro:{i.Id}:reason");
                    if (i.LastMessagePreview is not null && !protector.IsProtected(i.LastMessagePreview)) i.LastMessagePreview = protector.Protect(i.LastMessagePreview, $"intro:{i.Id}:preview");
                }
                await intros.UpdateRangeAsync(rows);
                converted += rows.Count;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var rows = messages.GetQueryable(m => !m.Body.StartsWith("v1."), ignoreQueryFilters: true).Take(Batch).ToList();
                if (rows.Count == 0) break;
                foreach (var m in rows) m.Body = protector.Protect(m.Body, $"intro:{m.IntroductionId}:message:{m.Id}");
                await messages.UpdateRangeAsync(rows);
                converted += rows.Count;
            }

            if (converted > 0) logger.LogInformation("Encrypted {Count} stored introduction texts that pre-dated encryption", converted);
        }
        catch (Exception e)
        {
            // Not fatal: unconverted text still reads correctly, and the next start tries again.
            logger.LogError(e, "Encrypting earlier introduction texts did not finish");
        }
    }
}

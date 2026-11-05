using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Realtime;

/// <summary>
/// The live stream of introduction notices. It is deliberately NOT a controller action: an ordinary request builds a database
/// context and a set of services that live until the request ends, which is nothing for a request that lasts milliseconds but is
/// real memory for one that lasts an hour per member. This path reads the member and institution from the login itself, checks the
/// feature switch once with a short lookup, releases everything, and then holds only a small queue per connection.
/// </summary>
public static class IntroductionStreamEndpoint
{
    public const string Path = "/api/v1/introductions/stream";
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(25);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly byte[] Hello = "retry: 3000\n: connected\n\n"u8.ToArray();
    private static readonly byte[] Ping = ": ping\n\n"u8.ToArray();

    // Whether an institution may open streams, remembered for a minute. After a restart every open page reconnects at once; without this
    // each would cost a database lookup. A switch-off or suspension therefore takes up to a minute to reach new connections (and any
    // existing stream stays until it closes or the login expires, which is within the hour).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool Allowed, DateTime Until)> Allowed = new();
    private static readonly TimeSpan AllowedFor = TimeSpan.FromSeconds(60);

    private static async Task<bool> MayStreamAsync(HttpContext http, string institutionId)
    {
        if (Allowed.TryGetValue(institutionId, out var known) && known.Until > DateTime.UtcNow) return known.Allowed;
        using var scope = http.RequestServices.CreateScope();   // released as soon as the lookup is done
        var institution = await scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<Institution>>().GetByIdAsync(institutionId, ignoreQueryFilters: true);
        var allowed = institution is not null && institution.Status != "Suspended" && !institution.DisabledFeatures.Contains(InstitutionFeatures.Introductions);
        Allowed[institutionId] = (allowed, DateTime.UtcNow + AllowedFor);
        return allowed;
    }

    /// <summary>Call after rate limiting and before tenant resolution, so the heavier per-request services are never created for a stream.</summary>
    public static IApplicationBuilder MapIntroductionStream(this IApplicationBuilder app)
        => app.Map(Path, branch => { branch.UseAuthentication(); branch.Run(HandleAsync); });

    private static async Task Reject(HttpContext http, int status)
    {
        http.Response.StatusCode = status;
        await Task.CompletedTask;
    }

    public static async Task HandleAsync(HttpContext http)
    {
        if (!HttpMethods.IsGet(http.Request.Method)) { await Reject(http, StatusCodes.Status405MethodNotAllowed); return; }
        var user = http.User;
        var memberId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        var institutionId = user.FindFirstValue("institution_id");
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(memberId) || string.IsNullOrEmpty(institutionId)) { await Reject(http, StatusCodes.Status401Unauthorized); return; }
        if (!user.IsInRole("Member")) { await Reject(http, StatusCodes.Status403Forbidden); return; }

        if (!await MayStreamAsync(http, institutionId)) { await Reject(http, StatusCodes.Status403Forbidden); return; }

        var events = http.RequestServices.GetRequiredService<IIntroductionEvents>();
        var response = http.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-store, no-transform";
        response.Headers["X-Accel-Buffering"] = "no";            // tells nginx not to hold the stream back
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        if (http.Features.Get<IHttpMinResponseDataRateFeature>() is { } rate) rate.MinDataRate = null;   // a quiet stream is not a slow client

        // The stream ends when the login does, so the page reconnects with a fresh one and a removed member cannot stay connected.
        var expiry = long.TryParse(user.FindFirstValue("exp"), out var exp) ? DateTimeOffset.FromUnixTimeSeconds(exp) : DateTimeOffset.UtcNow.AddMinutes(30);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        end.CancelAfter(expiry > DateTimeOffset.UtcNow ? expiry - DateTimeOffset.UtcNow : TimeSpan.Zero);

        using var subscription = events.Subscribe(institutionId, memberId);
        try
        {
            await response.Body.WriteAsync(Hello, end.Token);
            await response.Body.FlushAsync(end.Token);
            while (!end.IsCancellationRequested)
            {
                try
                {
                    // WaitAsync gives the heartbeat without creating a new token source every 25 seconds per connection.
                    if (!await subscription.Reader.WaitToReadAsync(end.Token).AsTask().WaitAsync(Heartbeat, end.Token)) break;   // replaced by a newer tab
                    while (subscription.Reader.TryRead(out var e))
                        await response.WriteAsync($"event: intro\ndata: {JsonSerializer.Serialize(e, Json)}\n\n", end.Token);
                }
                catch (TimeoutException)
                {
                    await response.Body.WriteAsync(Ping, end.Token);   // quiet for 25 seconds: a heartbeat
                }
                await response.Body.FlushAsync(end.Token);
            }
        }
        catch (OperationCanceledException) { /* the page went away or the login expired: normal */ }
    }
}

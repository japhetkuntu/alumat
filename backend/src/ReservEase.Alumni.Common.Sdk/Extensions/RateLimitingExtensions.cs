using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// Shared rate-limit policies for the three public-facing APIs. Every policy
/// partitions by caller IP *and* Host header (not by authenticated user —
/// these run early in the pipeline, before tenant resolution or auth, so no
/// user identity exists yet), so it works equally well for anonymous
/// endpoints (registration, login, public theme lookups).
///
/// Including Host alongside IP matters for institutions behind shared NAT
/// (campus WiFi, a corporate proxy): without it, every distinct member on
/// that connection collapses into one shared IP-only bucket, and once
/// combined traffic crosses the limit, *all* of them get 429s together —
/// including ones who individually did nothing wrong. Partitioning by
/// (Host, IP) at least stops unrelated institutions or public traffic that
/// happens to share an egress IP from throttling each other; it can't fully
/// separate individual users sharing both the same institution *and* the
/// same NAT IP, since no per-user signal exists before authentication — that
/// residual case is instead covered by sizing PublicReadPolicy generously
/// (cheap, largely cache-backed reads) and giving AuthPolicy a small queue
/// instead of an instant hard reject, so a legitimate burst (e.g. a class
/// full of students logging in at once) smooths out instead of mass-failing.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>Sensitive auth-flow endpoints (login, register, OTP, password reset) — tight limit, small queue so a legitimate simultaneous burst smooths out instead of mass-rejecting.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Unauthenticated read endpoints (public theme lookups, etc.) — generous, since a shared-NAT institution can easily have thousands of concurrent members hitting these.</summary>
    public const string PublicReadPolicy = "public-read";

    public static IServiceCollection AddAlumniRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 5,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }));

            options.AddPolicy(PublicReadPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 400,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    // X-Forwarded-For first, since these APIs sit behind nginx in every real
    // deployment — falling back to the socket address covers local dev. Host
    // is read directly off the request (cheap — no DB lookup), never from
    // TenantResolutionMiddleware's resolved Institution, since that runs
    // after UseRateLimiter() in the pipeline and would still cost a query
    // even for requests about to be rejected.
    private static string PartitionKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var ip = !string.IsNullOrWhiteSpace(forwardedFor)
            ? forwardedFor.Split(',')[0].Trim()
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var host = httpContext.Request.Host.Host.ToLowerInvariant();
        return $"{host}:{ip}";
    }
}

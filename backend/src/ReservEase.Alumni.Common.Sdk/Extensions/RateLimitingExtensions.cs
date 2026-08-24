using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// Shared rate-limit policies for the three public-facing APIs. Every policy
/// partitions by caller IP (not by authenticated user), so it works equally
/// well for anonymous endpoints (registration, login, public theme lookups)
/// and runs early in the pipeline, before tenant resolution or auth.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>Sensitive auth-flow endpoints (login, register, OTP, password reset) — tight limit, no queueing.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Unauthenticated read endpoints (public theme lookups, etc.) — generous but not unbounded.</summary>
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
                    QueueLimit = 0,
                }));

            options.AddPolicy(PublicReadPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    // X-Forwarded-For first, since these APIs sit behind nginx in every real
    // deployment — falling back to the socket address covers local dev.
    private static string PartitionKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

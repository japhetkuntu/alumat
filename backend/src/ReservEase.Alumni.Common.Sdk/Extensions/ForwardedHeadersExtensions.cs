using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// Makes <c>HttpContext.Connection.RemoteIpAddress</c> (and Request.Scheme)
/// trustworthy again behind nginx.
///
/// Without this, ASP.NET Core's built-in forwarded-header handling never
/// runs, so <c>RemoteIpAddress</c> is always nginx's own container IP for
/// every request — and any code that instead reads the raw
/// "X-Forwarded-For" header itself (as RateLimitingExtensions.PartitionKey
/// used to) is reading a value the CLIENT set, since nginx appends to
/// whatever arrived rather than replacing it. That let an attacker send a
/// fresh forged value on every request and get a brand-new rate-limit
/// bucket every time, fully defeating the login/OTP/password-reset brute
/// force protection.
///
/// <see cref="ForwardedHeadersMiddleware"/> fixes this correctly: it only
/// honors "X-Forwarded-For" when the immediate connection comes from a
/// KNOWN proxy, and once accepted, replaces (not appends to)
/// RemoteIpAddress with the client IP nginx reported. "Known proxy" here is
/// "anywhere on our own private Docker network" — the same trust boundary
/// TenantResolutionMiddleware already uses for its X-Internal-Tenant-Host
/// header, since institution-api/member-api/platform-api publish no host
/// ports (nginx is the only way in — see docker-compose.prod.yml).
/// </summary>
public static class ForwardedHeadersExtensions
{
    public static IApplicationBuilder UseAlumniForwardedHeaders(this IApplicationBuilder app)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            // A request may hop through more than one proxy layer (e.g. a
            // cloud load balancer in front of nginx) — without raising this,
            // the middleware only unwraps a single X-Forwarded-For entry.
            ForwardLimit = 2,
        };

        // RFC1918 + loopback + IPv6 unique-local — the same private-address
        // definition TenantResolutionMiddleware.IsPrivateOrLoopback uses,
        // duplicated here rather than shared across projects since
        // Common.Sdk doesn't (and shouldn't) depend on PostgresDb.Sdk.
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("fc00::"), 7));
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);

        return app.UseForwardedHeaders(options);
    }
}

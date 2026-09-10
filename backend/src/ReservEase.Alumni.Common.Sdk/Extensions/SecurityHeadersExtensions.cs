using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// Baseline response headers that cost nothing functionally but close off a
/// few cheap client-side attack classes: MIME-sniffing, being framed by a
/// third-party site (clickjacking), and leaking the full referrer URL
/// (which can contain tokens/ids in the query string) to external sites.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseAlumniSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            await next();
        });
    }
}

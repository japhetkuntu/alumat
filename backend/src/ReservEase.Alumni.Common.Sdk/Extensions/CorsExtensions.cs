using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Common.Sdk.Services;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

public static class CorsExtensions
{
    /// <summary>
    /// AllowAnyOrigin() can't be paired with AllowCredentials() (needed now that
    /// auth travels as cookies), and a fixed origin list can't cover this
    /// platform's production shape either — institutions get arbitrary
    /// subdomains, minted on the fly, not a fixed set. So the real check is
    /// "same suffix as the configured base domain" (mirrors
    /// TenantResolutionMiddleware's own host matching and the frontend's
    /// google-auth bridge isAllowedReturnUrl), with an explicit list layered on
    /// top only for origins that don't fit that shape — local dev, where every
    /// app sits on plain "localhost" at a different port rather than a real
    /// subdomain — plus a live custom-domain cache for institutions that don't
    /// use a base-domain subdomain at all (see Institution.CustomDomain /
    /// TenantResolutionMiddleware's byCustomDomain lookup). Common.Sdk itself
    /// has no database access (PostgresDb.Sdk already depends on Common.Sdk,
    /// so the reverse would be circular) — customDomainCache is populated by
    /// a background refresher registered in each API's own Program.cs, which
    /// does have DB access; pass the SAME instance to both so they share state.
    /// </summary>
    public static IServiceCollection AddTenantAwareCors(
        this IServiceCollection services, IConfiguration config,
        ICustomDomainCache? customDomainCache = null, Action<CorsPolicyBuilder>? configureExtra = null)
    {
        var baseDomain = config["PlatformBaseDomain"];
        var extraOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, baseDomain, extraOrigins, customDomainCache))
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod();
            configureExtra?.Invoke(policy);
        }));

        return services;
    }

    private static bool IsAllowedOrigin(string origin, string? baseDomain, IReadOnlyCollection<string> extraOrigins, ICustomDomainCache? customDomainCache)
    {
        if (extraOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;

        try
        {
            var uri = new Uri(origin);
            if (uri.Scheme != "https" && uri.Scheme != "http") return false;

            if (!string.IsNullOrWhiteSpace(baseDomain) &&
                (uri.Host.Equals(baseDomain, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + baseDomain, StringComparison.OrdinalIgnoreCase)))
                return true;

            return customDomainCache?.Contains(uri.Host) ?? false;
        }
        catch
        {
            return false;
        }
    }
}

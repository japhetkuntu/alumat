using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Sdk.Middleware;

/// <summary>
/// Resolves the current request's <see cref="Institution"/> (tenant) from the
/// Host header — by custom domain, then by "{slug}.{PlatformBaseDomain}" — and
/// sets it on <see cref="ICurrentTenantService"/> before any downstream
/// DbContext query runs (the query filter on ITenantScoped entities depends on it).
///
/// In Development only, an explicit "X-Institution-Slug" header is honored as a
/// substitute for subdomain routing, so institutions can be reached locally
/// before a real platform domain + wildcard DNS/SSL exists. It is never
/// consulted in any other environment and never overrides a genuine
/// custom-domain or subdomain match — production tenant isolation is
/// Host-header-only, exactly as before.
///
/// Separately, an "X-Internal-Tenant-Host" header is honored in every
/// environment, but ONLY from loopback-originated requests — this exists
/// because Node's fetch() (used by each frontend's server-side tenant-theme
/// fetch, see apps/*/src/lib/theme.ts) silently ignores any caller-supplied
/// "Host" header and always sends the request URL's own host instead, so the
/// browser's real subdomain has to be forwarded some other way for that one
/// internal, same-machine call. Safe to trust unconditionally because these
/// loopback ports are never reachable from the internet (nginx/firewall only
/// expose 80/443) — nothing external can ever forge this header.
///
/// Falls back to a configured "DefaultInstitutionSlug" when nothing else
/// matches — this is what keeps every existing request (today's fixed
/// production hostnames, docker-internal calls) behaving exactly as a
/// single-tenant app until wildcard DNS/SSL is live.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string DevSlugHeader = "X-Institution-Slug";
    private const string InternalTenantHostHeader = "X-Internal-Tenant-Host";

    public async Task InvokeAsync(
        HttpContext context, AlumniDbContext db, ICurrentTenantService currentTenant,
        IConfiguration config, IWebHostEnvironment env)
    {
        var baseDomain = config["PlatformBaseDomain"];

        async Task<Institution?> ResolveByHostAsync(string h)
        {
            var byCustomDomain = await db.Institutions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.CustomDomain == h);
            if (byCustomDomain is not null) return byCustomDomain;

            if (!string.IsNullOrWhiteSpace(baseDomain) && h.EndsWith("." + baseDomain))
            {
                var slug = h[..^(baseDomain.Length + 1)];
                return await db.Institutions.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Slug == slug);
            }

            return null;
        }

        var host = context.Request.Host.Host.ToLowerInvariant();
        var institution = await ResolveByHostAsync(host);

        var remoteIp = context.Connection.RemoteIpAddress;
        if (institution is null && remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp) &&
            context.Request.Headers.TryGetValue(InternalTenantHostHeader, out var internalHostValues))
        {
            var internalHost = internalHostValues.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(internalHost))
                institution = await ResolveByHostAsync(internalHost);
        }

        if (institution is null && env.IsDevelopment() &&
            context.Request.Headers.TryGetValue(DevSlugHeader, out var devSlugValues))
        {
            var devSlug = devSlugValues.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(devSlug))
            {
                institution = await db.Institutions.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.Slug == devSlug);
            }
        }

        if (institution is null)
        {
            var defaultSlug = config["DefaultInstitutionSlug"];
            if (!string.IsNullOrWhiteSpace(defaultSlug))
            {
                institution = await db.Institutions.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.Slug == defaultSlug);
            }
        }

        if (institution is not null)
        {
            currentTenant.SetInstitutionId(institution.Id, institution.Slug);
            context.Items["Institution"] = institution;
        }

        await next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}

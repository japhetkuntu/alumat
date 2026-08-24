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
/// Falls back to a configured "DefaultInstitutionSlug" when nothing else
/// matches — this is what keeps every existing request (today's fixed
/// production hostnames, docker-internal calls) behaving exactly as a
/// single-tenant app until wildcard DNS/SSL is live.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string DevSlugHeader = "X-Institution-Slug";

    public async Task InvokeAsync(
        HttpContext context, AlumniDbContext db, ICurrentTenantService currentTenant,
        IConfiguration config, IWebHostEnvironment env)
    {
        var host = context.Request.Host.Host.ToLowerInvariant();
        var baseDomain = config["PlatformBaseDomain"];

        var institution = await db.Institutions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.CustomDomain == host);

        if (institution is null && !string.IsNullOrWhiteSpace(baseDomain) && host.EndsWith("." + baseDomain))
        {
            var slug = host[..^(baseDomain.Length + 1)];
            institution = await db.Institutions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Slug == slug);
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
            currentTenant.SetInstitutionId(institution.Id);
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

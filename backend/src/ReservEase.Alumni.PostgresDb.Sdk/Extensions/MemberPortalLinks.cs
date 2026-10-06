namespace ReservEase.Alumni.PostgresDb.Sdk.Extensions;

/// <summary>
/// The one place that decides what address members use to reach an institution's portal, so every link we hand out
/// (share buttons, emails, notifications, onboarding messages) agrees. An institution on its own domain is reached
/// there; everyone else on <c>{slug}.{MemberPortalBaseDomain}</c>.
/// </summary>
public static class MemberPortalLinks
{
    /// <summary>The institution's member-portal origin (no trailing slash), or null when neither a custom domain nor a base domain is configured.</summary>
    public static string? Url(string? slug, string? customDomain, string? baseDomain)
    {
        var custom = NormalizeHost(customDomain);
        if (custom is not null) return $"https://{custom}";
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(baseDomain)) return null;
        return $"https://{slug.Trim()}.{baseDomain.Trim().Trim('.')}";
    }

    /// <summary>Same as <see cref="Url"/>, with an empty string instead of null for callers that concatenate.</summary>
    public static string UrlOrEmpty(string? slug, string? customDomain, string? baseDomain) => Url(slug, customDomain, baseDomain) ?? string.Empty;

    /// <summary>Accepts what an admin might have typed ("https://alumni.school.edu/") and keeps just the host.</summary>
    private static string? NormalizeHost(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;
        var host = domain.Trim();
        var scheme = host.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) host = host[(scheme + 3)..];
        host = host.Split('/', '?', '#')[0].Trim().TrimEnd('.');
        return host.Length == 0 ? null : host.ToLowerInvariant();
    }
}

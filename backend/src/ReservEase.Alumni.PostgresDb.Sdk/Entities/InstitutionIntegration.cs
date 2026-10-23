namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// The settings platform staff give an institution that onboards its own students through a partner portal: whether the
/// feature is on, how long a registration link works, where members are sent back to, and what the member portal tells them
/// meanwhile. One row per institution. Platform-managed, so not tenant scoped: only Platform.Api writes it, and the other
/// APIs read it for one institution by id.
/// </summary>
public class InstitutionIntegration : BaseEntity
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Off by default. While off, the status API refuses every call and no registration link is honoured.</summary>
    public bool Enabled { get; set; }

    /// <summary>The partner portal's own name, e.g. "UMaT Off-boarding Portal". Used on the banner button and on an expired link's page.</summary>
    public string PortalName { get; set; } = string.Empty;

    /// <summary>Where the member is sent back to. HTTPS only.</summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>Plain text shown in the member portal banner. Never rendered as HTML.</summary>
    public string BannerMessage { get; set; } = string.Empty;

    /// <summary>How long, from registration, the banner stays up.</summary>
    public int BannerHours { get; set; } = 72;

    /// <summary>How long a registration link works after it is issued.</summary>
    public int LinkExpiryHours { get; set; } = 72;

    /// <summary>How long a "this student already exists" answer is remembered, so repeat checks do not reach the database.</summary>
    public int StatusCacheDays { get; set; } = 30;
}

/// <summary>An API key a partner's server uses to call the status check. Only a hash is stored; the key itself is shown once.</summary>
public class IntegrationApiKey : BaseEntity
{
    public string InstitutionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>The first characters of the key, so staff can tell keys apart without being able to use them.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>SHA-256 of the whole key, hex. Keys are long random values, so a fast hash is enough and keeps every call cheap.</summary>
    public string KeyHash { get; set; } = string.Empty;

    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

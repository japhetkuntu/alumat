using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// An institution's own website: one single scrolling page of sections (see <see cref="Website.WebsiteDocument"/>), edited as a draft and made
/// public with Publish. One row per institution. While <see cref="IsLive"/> is false the member app keeps showing its built-in landing page,
/// so an institution that never opens the builder sees no change.
/// </summary>
public class InstitutionWebsite : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>The page as the admin is editing it. Always valid and cleaned (see WebsiteDocumentValidator) before it is stored.</summary>
    public string DraftJson { get; set; } = "{}";

    /// <summary>Counts every draft save. A save that names a different revision than the stored one is refused, so two admins editing at once are warned, not silently overwritten.</summary>
    public int DraftRevision { get; set; }

    /// <summary>What visitors see. Null until the first publish.</summary>
    public string? PublishedJson { get; set; }

    /// <summary>The draft revision that was published, so the editor can say "unpublished changes" without comparing documents.</summary>
    public int PublishedRevision { get; set; }

    /// <summary>On = the member app serves this page at the institution's address. Off = the built-in landing page.</summary>
    public bool IsLive { get; set; }

    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
}

/// <summary>One earlier publish, kept so an admin can put it back. Only the most recent few are kept per institution.</summary>
public class InstitutionWebsiteVersion : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string DocumentJson { get; set; } = "{}";
    public string PublishedByName { get; set; } = string.Empty;
}

using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One photo belonging to a <see cref="PhotoAlbum"/> — its own table (not
/// embedded jsonb on the parent) because photos get added incrementally
/// across many separate admin sessions, not resubmitted as a whole list on
/// every edit the way StoreProduct.ImageUrls is.
/// </summary>
public class AlbumPhoto : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string AlbumId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

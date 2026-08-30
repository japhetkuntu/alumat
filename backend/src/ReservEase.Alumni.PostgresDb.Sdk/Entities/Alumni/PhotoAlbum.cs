using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A named collection of <see cref="AlbumPhoto"/> rows an institution admin
/// creates once and then adds photos to incrementally over many separate
/// sessions — not a single bulk upload at creation time, which is why photos
/// are their own relational child table rather than a jsonb list here.
/// </summary>
public class PhotoAlbum : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Defaults to the first photo added when null; an admin can override it
    /// to any of the album's own photo URLs. Kept in sync automatically when
    /// the photo it points at is deleted (see AlbumsController.DeletePhoto).
    /// </summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>Denormalized count kept in sync on photo add/remove, so listing albums never needs a COUNT query per row.</summary>
    public int PhotoCount { get; set; }
}

using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Models;

public class CreateAlbumRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateAlbumRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>When provided, must match one of the album's existing AlbumPhoto URLs — validated server-side.</summary>
    public string? CoverImageUrl { get; set; }
}

public class AddAlbumPhotosRequest
{
    public List<IFormFile> Photos { get; set; } = [];
}

public class AddAlbumPhotosResponse
{
    public PhotoAlbumDto Album { get; set; } = null!;
    public List<AlbumPhotoDto> AddedPhotos { get; set; } = [];
}

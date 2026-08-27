using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IUploadService
{
    /// <summary>institutionSlug files this upload under that institution's own subfolder — pass it whenever the upload is known to belong to one institution (e.g. editing its branding); omit for platform-wide uploads with no single owning institution.</summary>
    Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file, string? institutionSlug = null);
}

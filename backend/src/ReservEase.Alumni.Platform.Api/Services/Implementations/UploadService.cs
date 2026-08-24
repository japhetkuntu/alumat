using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Image uploads for platform staff — used when onboarding an institution
/// or editing its branding, so staff don't have to hand-paste a hosted logo/
/// icon URL. Institution and Member portals have their own equivalent
/// per-API service; this one deliberately supports images only (platform
/// staff have no use for general file uploads).
/// </summary>
public class UploadService(
    IStorageService storageService,
    ILogger<UploadService> logger) : IUploadService
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };

    private const long MaxImageSize = 5 * 1024 * 1024;

    public async Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("No file provided");

        if (!AllowedImageTypes.Contains(file.ContentType))
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("Invalid image type. Allowed: JPEG, PNG, GIF, WebP, SVG");

        if (file.Length > MaxImageSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("Image exceeds 5MB limit");

        var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var url = await storageService.UploadFileAsync(file, objectName);

        logger.LogInformation("Platform image uploaded: {ObjectName}", objectName);
        return new UploadResult(url).ToOkApiResponse();
    }
}

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
    // SVG deliberately excluded — see FileContentValidator's doc comment.
    private static readonly HashSet<string> AllowedImageTypes = new(UploadFileTypes.ImageExtensions.Keys, StringComparer.OrdinalIgnoreCase);

    private const long MaxImageSize = 5 * 1024 * 1024;

    public async Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file, string? institutionSlug = null)
    {
        if (file is null || file.Length == 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("No file provided");

        if (!AllowedImageTypes.Contains(file.ContentType))
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("Invalid image type. Allowed: JPEG, PNG, GIF, WebP");

        if (file.Length > MaxImageSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("Image exceeds 5MB limit");

        if (!await FileContentValidator.LooksLikeDeclaredImageTypeAsync(file))
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("File content doesn't match its declared image type.");

        var objectName = $"{Guid.NewGuid():N}{UploadFileTypes.SafeExtension(file.ContentType)}";
        var url = await storageService.UploadFileAsync(file, objectName, institutionSlug: institutionSlug ?? "");

        logger.LogInformation("Platform image uploaded: {ObjectName}", objectName);
        return new UploadResult(url).ToOkApiResponse();
    }
}

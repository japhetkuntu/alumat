using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Storage.Sdk.Services;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class UploadService(
    IStorageService storageService,
    ILogger<UploadService> logger) : IUploadService
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };

    private const long MaxImageSize = 5 * 1024 * 1024;
    private const long MaxFileSize = 20 * 1024 * 1024;

    public async Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("No file provided");

        if (!AllowedImageTypes.Contains(file.ContentType))
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("Invalid image type. Allowed: JPEG, PNG, GIF, WebP, SVG");

        if (file.Length > MaxImageSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("Image exceeds 5MB limit");

        var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        await storageService.UploadFileAsync(file, objectName);

        logger.LogInformation("Image uploaded: {ObjectName}", objectName);
        return new UploadResult(objectName).ToOkApiResponse();
    }

    public async Task<IApiResponse<UploadResult>> UploadFileAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("No file provided");

        if (file.Length > MaxFileSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("File exceeds 20MB limit");

        var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        await storageService.UploadFileAsync(file, objectName);

        logger.LogInformation("File uploaded: {ObjectName}", objectName);
        return new UploadResult(objectName).ToOkApiResponse();
    }
}

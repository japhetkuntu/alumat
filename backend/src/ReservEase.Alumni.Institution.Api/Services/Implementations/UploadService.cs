using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class UploadService(
    IStorageService storageService,
    ICurrentTenantService currentTenant,
    ILogger<UploadService> logger) : IUploadService
{
    // SVG deliberately excluded — see FileContentValidator's doc comment.
    private static readonly HashSet<string> AllowedImageTypes = new(UploadFileTypes.ImageExtensions.Keys, StringComparer.OrdinalIgnoreCase);

    // Any authenticated institution staffer (any role) can reach this
    // endpoint — an explicit allowlist of genuinely document-shaped types
    // for the Resources library this backs, rather than accepting anything,
    // so it can't become a way to host and publicly serve arbitrary
    // executable/script-bearing content (e.g. .html, .svg, .js) with a
    // public-read ACL.
    private static readonly HashSet<string> AllowedFileTypes = new(
        UploadFileTypes.ImageExtensions.Keys.Concat(UploadFileTypes.DocumentExtensions.Keys), StringComparer.OrdinalIgnoreCase);

    private const long MaxImageSize = 5 * 1024 * 1024;
    private const long MaxFileSize = 50 * 1024 * 1024;

    public async Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file)
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
        var url = await storageService.UploadFileAsync(file, objectName, institutionSlug: currentTenant.InstitutionSlug ?? "");

        logger.LogInformation("Image uploaded: {ObjectName}", objectName);
        return new UploadResult(url).ToOkApiResponse();
    }

    public async Task<IApiResponse<UploadResult>> UploadFileAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("No file provided");

        if (file.Length > MaxFileSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("File exceeds 50MB limit");

        if (!AllowedFileTypes.Contains(file.ContentType))
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("This file type isn't supported.");

        // AllowedFileTypes includes the same image types UploadImageAsync accepts —
        // those get the same magic-byte check here, so this endpoint can't be used
        // to bypass it by spoofing an image Content-Type on a non-image file.
        if (AllowedImageTypes.Contains(file.ContentType) && !await FileContentValidator.LooksLikeDeclaredImageTypeAsync(file))
            return ApiResponseExtensions.ToBadRequestApiResponse<UploadResult>("File content doesn't match its declared type.");

        // The stored extension is derived from the validated Content-Type,
        // never the client-supplied filename — a client claiming
        // "application/pdf" with a filename of "x.html" would otherwise get
        // an .html file served back with a public-read ACL.
        var objectName = $"{Guid.NewGuid():N}{UploadFileTypes.SafeExtension(file.ContentType)}";
        var url = await storageService.UploadFileAsync(file, objectName, institutionSlug: currentTenant.InstitutionSlug ?? "");

        logger.LogInformation("File uploaded: {ObjectName}", objectName);
        return new UploadResult(url).ToOkApiResponse();
    }
}

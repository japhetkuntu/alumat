using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// File upload endpoints for members.
/// </summary>
[Authorize]
public class UploadsController(
    IStorageService storageService, ICurrentTenantService currentTenant, ILogger<UploadsController> logger) : DefaultController
{
    private const string FolderName = "alumni";
    // SVG deliberately excluded — see FileContentValidator's doc comment.
    private static readonly HashSet<string> AllowedImageTypes = new(UploadFileTypes.ImageExtensions.Keys, StringComparer.OrdinalIgnoreCase);
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Upload a single image file (max 5MB). Returns the stored filename.
    /// </summary>
    [HttpPost("image")]
    [SwaggerOperation(Summary = "Upload image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<UploadResult>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("No file provided").ToActionResult();

        if (!AllowedImageTypes.Contains(file.ContentType))
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid image type. Allowed: JPEG, PNG, GIF, WebP").ToActionResult();

        if (file.Length > MaxImageSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Image exceeds 5MB limit").ToActionResult();

        if (!await FileContentValidator.LooksLikeDeclaredImageTypeAsync(file))
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("File content doesn't match its declared image type.").ToActionResult();

        var objectName = $"{Guid.NewGuid():N}{UploadFileTypes.SafeExtension(file.ContentType)}";
        var url = await storageService.UploadFileAsync(file, objectName, FolderName, currentTenant.InstitutionSlug ?? "");

        logger.LogInformation("Image uploaded by member: {ObjectName}", objectName);
        return new UploadResult(url).ToOkApiResponse().ToActionResult();
    }
}

public record UploadResult(string Url);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Storage.Sdk.Services;

namespace Umat.Alumni.Member.Api.Controllers;

/// <summary>
/// File upload endpoints for members.
/// </summary>
[Authorize]
public class UploadsController(IStorageService storageService, ILogger<UploadsController> logger) : DefaultController
{
    private const string FolderName = "alumni";
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };
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
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid image type. Allowed: JPEG, PNG, GIF, WebP, SVG").ToActionResult();

        if (file.Length > MaxImageSize)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Image exceeds 5MB limit").ToActionResult();

        var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        await storageService.UploadFileAsync(file, objectName, FolderName);

        logger.LogInformation("Image uploaded by member: {ObjectName}", objectName);
        return new UploadResult(objectName).ToOkApiResponse().ToActionResult();
    }
}

public record UploadResult(string FileName);

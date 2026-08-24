using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Image uploads for platform staff — e.g. an institution's logo/icon while
/// onboarding or editing branding, instead of pasting an externally-hosted URL.
/// </summary>
[Authorize(Roles = "SuperAdmin,Support,Sales")]
public class UploadsController(IUploadService uploadService) : DefaultController
{
    /// <summary>Upload a single image file (max 5MB). Returns its public URL.</summary>
    [HttpPost("image")]
    [SwaggerOperation(Summary = "Upload image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<UploadResult>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        var result = await uploadService.UploadImageAsync(file);
        return result.ToActionResult();
    }
}

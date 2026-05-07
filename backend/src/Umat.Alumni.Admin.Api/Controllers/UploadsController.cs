using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Controllers;

/// <summary>
/// File upload endpoints.
/// </summary>
[Authorize]
public class UploadsController(IUploadService uploadService) : DefaultController
{

    /// <summary>
    /// Upload a single image file (max 5MB). Returns the stored filename.
    /// </summary>
    [HttpPost("image")]
    [SwaggerOperation(Summary = "Upload image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<UploadResult>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        var result = await uploadService.UploadImageAsync(file);
        return result.ToActionResult();
    }

    /// <summary>
    /// Upload a general file (max 20MB). Returns the stored filename.
    /// </summary>
    [HttpPost("file")]
    [SwaggerOperation(Summary = "Upload file")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<UploadResult>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        var result = await uploadService.UploadFileAsync(file);
        return result.ToActionResult();
    }
}

public record UploadResult(string FileName);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Browse this institution's photo albums and their photos.</summary>
[Authorize]
[RequireFeature(InstitutionFeatures.PhotoAlbums)]
public class AlbumsController(IAlbumService albumService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List photo albums")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PhotoAlbumDto>>))]
    public async Task<IActionResult> GetAlbums([FromQuery] PhotoAlbumFilter filter)
    {
        var result = await albumService.GetAlbumsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get a photo album by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PhotoAlbumDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetAlbum(string id)
    {
        var result = await albumService.GetAlbumByIdAsync(id);
        return result.ToActionResult();
    }

    [HttpGet("{id}/photos")]
    [SwaggerOperation(Summary = "List an album's photos", Description = "Its own paged sub-resource so a lightbox can lazy-load beyond an initial page.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<AlbumPhotoDto>>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetAlbumPhotos(string id, [FromQuery] AlbumPhotoFilter filter)
    {
        var result = await albumService.GetAlbumPhotosAsync(id, filter);
        return result.ToActionResult();
    }
}

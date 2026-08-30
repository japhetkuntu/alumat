using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>
/// Manage this institution's photo albums — admins create an album once and
/// add photos to it incrementally over many separate sessions afterward.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
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
    [SwaggerOperation(Summary = "List an album's photos", Description = "Paged sub-resource — lets the admin gallery reload the full photo list after a page refresh.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<AlbumPhotoDto>>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetAlbumPhotos(string id, [FromQuery] AlbumPhotoFilter filter)
    {
        var result = await albumService.GetAlbumPhotosAsync(id, filter);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create a photo album", Description = "No photos yet — add them afterward via POST /albums/{id}/photos.")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<PhotoAlbumDto>))]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumRequest request)
    {
        var admin = User.GetAccount();
        var result = await albumService.CreateAlbumAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update a photo album", Description = "CoverImageUrl, if provided, must match one of the album's existing photo URLs.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PhotoAlbumDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateAlbum(string id, [FromBody] UpdateAlbumRequest request)
    {
        var admin = User.GetAccount();
        var result = await albumService.UpdateAlbumAsync(id, request, admin);
        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete a photo album", Description = "Deletes the album and all of its photos.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteAlbum(string id)
    {
        var result = await albumService.DeleteAlbumAsync(id);
        return result.ToActionResult();
    }

    [HttpPost("{id}/photos")]
    [SwaggerOperation(Summary = "Add photos to an album", Description = "Uploads the given photos and appends them — sets the album's cover photo if it had none yet.")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<AddAlbumPhotosResponse>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> AddPhotos(string id, [FromForm] AddAlbumPhotosRequest request)
    {
        var admin = User.GetAccount();
        var result = await albumService.AddPhotosAsync(id, request, admin);
        return result.ToActionResult();
    }

    [HttpDelete("{id}/photos/{photoId}")]
    [SwaggerOperation(Summary = "Delete a photo from an album", Description = "Reassigns the album's cover photo if it pointed at the deleted photo.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeletePhoto(string id, string photoId)
    {
        var result = await albumService.DeletePhotoAsync(id, photoId);
        return result.ToActionResult();
    }
}

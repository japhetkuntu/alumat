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
/// Manage the "Alumni Services" catalog (transcripts, attestation letters,
/// certificate reissue, English proficiency letters, or anything else the
/// institution wants to define) and work the queue of member requests.
/// SuperAdmin only, per the institution's own choice — matches Store.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[RequireFeature(InstitutionFeatures.Services)]
public class ServicesController(IServiceService serviceService) : DefaultController
{
    [HttpGet("types")]
    [SwaggerOperation(Summary = "List service types")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ServiceTypeDto>>))]
    public async Task<IActionResult> GetServiceTypes([FromQuery] ServiceTypeFilter filter)
    {
        var result = await serviceService.GetServiceTypesAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("types/{serviceTypeId}")]
    [SwaggerOperation(Summary = "Get service type by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceTypeDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetServiceType(string serviceTypeId)
    {
        var result = await serviceService.GetServiceTypeByIdAsync(serviceTypeId);
        return result.ToActionResult();
    }

    [HttpPost("types")]
    [SwaggerOperation(Summary = "Create a service type")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<ServiceTypeDto>))]
    public async Task<IActionResult> CreateServiceType([FromBody] CreateServiceTypeRequest request)
    {
        var admin = User.GetAccount();
        var result = await serviceService.CreateServiceTypeAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpPut("types/{serviceTypeId}")]
    [SwaggerOperation(Summary = "Update a service type")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceTypeDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateServiceType(string serviceTypeId, [FromBody] UpdateServiceTypeRequest request)
    {
        request.ServiceTypeId = serviceTypeId;
        var admin = User.GetAccount();
        var result = await serviceService.UpdateServiceTypeAsync(request, admin);
        return result.ToActionResult();
    }

    [HttpDelete("types/{serviceTypeId}")]
    [SwaggerOperation(Summary = "Delete (or archive, if it has requests) a service type")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteServiceType(string serviceTypeId)
    {
        var result = await serviceService.DeleteServiceTypeAsync(serviceTypeId);
        return result.ToActionResult();
    }

    [HttpGet("requests")]
    [SwaggerOperation(Summary = "List member service requests")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ServiceRequestDto>>))]
    public async Task<IActionResult> GetRequests([FromQuery] ServiceRequestFilter filter)
    {
        var result = await serviceService.GetRequestsAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("requests/{requestId}")]
    [SwaggerOperation(Summary = "Get a service request by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceRequestDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetRequest(string requestId)
    {
        var result = await serviceService.GetRequestByIdAsync(requestId);
        return result.ToActionResult();
    }

    [HttpPatch("requests/{requestId}")]
    [SwaggerOperation(Summary = "Advance a request's stage, add a note, and/or attach a file back to the member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceRequestDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateRequest(string requestId, [FromForm] UpdateServiceRequestRequest request)
    {
        var admin = User.GetAccount();
        var result = await serviceService.UpdateRequestAsync(requestId, request, admin);
        return result.ToActionResult();
    }
}

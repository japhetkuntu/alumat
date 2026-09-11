using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Browse alumni services and request/pay for one — same platform-fee model as Contributions and Store.</summary>
[Authorize]
[RequireFeature(InstitutionFeatures.Services)]
public class ServicesController(IServiceRequestService serviceRequestService) : DefaultController
{
    [HttpGet("types")]
    [SwaggerOperation(Summary = "List available services")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ServiceTypeDto>>))]
    public async Task<IActionResult> GetServiceTypes([FromQuery] ServiceTypeFilter filter)
    {
        var result = await serviceRequestService.GetServiceTypesAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("types/{serviceTypeId}")]
    [SwaggerOperation(Summary = "Get a service by ID")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceTypeDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetServiceType(string serviceTypeId)
    {
        var result = await serviceRequestService.GetServiceTypeByIdAsync(serviceTypeId);
        return result.ToActionResult();
    }

    /// <summary>
    /// File-type field answers are submitted as their own form parts, named
    /// exactly as the field's Key — see CreateServiceRequestRequest's doc
    /// comment for why. Everything else binds normally from the same
    /// multipart form.
    /// </summary>
    [HttpPost("requests")]
    [SwaggerOperation(Summary = "Request a service (pays if priced, submits directly if free)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceRequestCheckoutResponse>))]
    public async Task<IActionResult> CreateRequest([FromForm] CreateServiceRequestRequest request)
    {
        var member = User.GetAccount();

        var attachments = new Dictionary<string, IFormFile>();
        foreach (var file in Request.Form.Files)
        {
            if (file.Name != nameof(CreateServiceRequestRequest.AnswersJson) && file.Name != nameof(CreateServiceRequestRequest.ServiceTypeId) && file.Name != nameof(CreateServiceRequestRequest.CallbackUrl))
                attachments[file.Name] = file;
        }

        var result = await serviceRequestService.CreateRequestAsync(request, attachments, member);
        return result.ToActionResult();
    }

    [HttpGet("requests/{reference}/status")]
    [SwaggerOperation(Summary = "Poll a paid request's payment status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ServiceRequestStatusResponse>))]
    public async Task<IActionResult> GetRequestStatus(string reference)
    {
        var member = User.GetAccount();
        var result = await serviceRequestService.GetRequestStatusAsync(reference, member);
        return result.ToActionResult();
    }

    [HttpGet("requests")]
    [SwaggerOperation(Summary = "List my service requests")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ServiceRequestDto>>))]
    public async Task<IActionResult> GetMyRequests([FromQuery] ServiceRequestFilter filter)
    {
        var member = User.GetAccount();
        var result = await serviceRequestService.GetMyRequestsAsync(filter, member.Id);
        return result.ToActionResult();
    }
}

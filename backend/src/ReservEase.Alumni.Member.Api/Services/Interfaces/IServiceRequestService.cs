using Microsoft.AspNetCore.Http;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IServiceRequestService
{
    Task<IApiResponse<PgPagedResult<ServiceTypeDto>>> GetServiceTypesAsync(ServiceTypeFilter filter);
    Task<IApiResponse<ServiceTypeDto>> GetServiceTypeByIdAsync(string serviceTypeId);
    Task<IApiResponse<ServiceRequestCheckoutResponse>> CreateRequestAsync(CreateServiceRequestRequest request, Dictionary<string, IFormFile> attachments, AuthData member);
    Task<IApiResponse<object>> ProcessPaystackCallbackAsync(string reference, string rawBody);
    Task<IApiResponse<ServiceRequestStatusResponse>> GetRequestStatusAsync(string reference, AuthData member);
    Task<IApiResponse<PgPagedResult<ServiceRequestDto>>> GetMyRequestsAsync(ServiceRequestFilter filter, string memberId);
    /// <summary>Cheap existence check the Paystack webhook dispatcher uses to decide whether a reference belongs to a service request (vs Store or Contribution).</summary>
    Task<bool> OwnsReferenceAsync(string reference);
}

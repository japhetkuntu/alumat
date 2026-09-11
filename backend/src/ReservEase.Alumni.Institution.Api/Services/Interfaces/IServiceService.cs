using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IServiceService
{
    Task<IApiResponse<PgPagedResult<ServiceTypeDto>>> GetServiceTypesAsync(ServiceTypeFilter filter);
    Task<IApiResponse<ServiceTypeDto>> GetServiceTypeByIdAsync(string serviceTypeId);
    Task<IApiResponse<ServiceTypeDto>> CreateServiceTypeAsync(CreateServiceTypeRequest request, AuthData admin);
    Task<IApiResponse<ServiceTypeDto>> UpdateServiceTypeAsync(UpdateServiceTypeRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteServiceTypeAsync(string serviceTypeId);

    Task<IApiResponse<PgPagedResult<ServiceRequestDto>>> GetRequestsAsync(ServiceRequestFilter filter);
    Task<IApiResponse<ServiceRequestDto>> GetRequestByIdAsync(string requestId);
    Task<IApiResponse<ServiceRequestDto>> UpdateRequestAsync(string requestId, UpdateServiceRequestRequest request, AuthData admin);
}

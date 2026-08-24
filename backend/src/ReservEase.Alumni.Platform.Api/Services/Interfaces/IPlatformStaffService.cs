using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlatformStaffService
{
    Task<IApiResponse<PgPagedResult<PlatformStaffResponse>>> GetStaffAsync(int page, int pageSize, string? search);
    Task<IApiResponse<PlatformStaffResponse>> CreateAsync(CreatePlatformStaffRequest request, string createdBy, string actorName);
    Task<IApiResponse<PlatformStaffResponse>> UpdateAsync(string id, UpdatePlatformStaffRequest request, string updatedBy, string actorName);
}

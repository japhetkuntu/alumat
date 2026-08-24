using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IPlanService
{
    Task<IApiResponse<List<PlanResponse>>> GetPlansAsync();
    Task<IApiResponse<PlanResponse>> CreateAsync(CreatePlanRequest request, string actorId, string actorName);
}

using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IOnboardingLeadService
{
    Task<IApiResponse<List<OnboardingLeadResponse>>> GetLeadsAsync(string? status);
    Task<IApiResponse<OnboardingLeadResponse>> GetLeadByIdAsync(string id);
    Task<IApiResponse<OnboardingLeadResponse>> CreateAsync(CreateOnboardingLeadRequest request);
    Task<IApiResponse<OnboardingLeadResponse>> UpdateStatusAsync(string id, UpdateOnboardingLeadStatusRequest request, string actorId, string actorName);
    Task<IApiResponse<OnboardingLeadResponse>> AddNoteAsync(string id, AddInternalNoteRequest request, string actorId, string actorName);
}

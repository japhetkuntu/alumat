using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface ISupportCaseService
{
    Task<IApiResponse<List<SupportCaseResponse>>> GetCasesAsync(string? status);
    Task<IApiResponse<SupportCaseResponse>> CreateAsync(CreateSupportCaseRequest request, string createdBy);
    Task<IApiResponse<SupportCaseResponse>> UpdateStatusAsync(string id, UpdateSupportCaseStatusRequest request, string actorId, string actorName);
    Task<IApiResponse<SupportCaseResponse>> AddNoteAsync(string id, AddInternalNoteRequest request, string actorId, string actorName);
}

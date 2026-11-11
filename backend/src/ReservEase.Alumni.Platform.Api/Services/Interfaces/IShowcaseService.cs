using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

/// <summary>Which institutions the AlumUnion website names as real examples, and which of their numbers it may show. Platform staff only.</summary>
public interface IShowcaseService
{
    Task<IApiResponse<List<ShowcaseEntry>>> GetAsync();
    Task<IApiResponse<ShowcaseEntry>> SaveAsync(string institutionId, SaveShowcaseRequest request, string actorId, string actorName);
}

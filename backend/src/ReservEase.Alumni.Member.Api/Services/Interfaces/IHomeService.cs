using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IHomeService
{
    Task<IApiResponse<HomeFeedDto>> GetFeedAsync(string memberId, IReadOnlyCollection<string> disabledFeatures);
    Task<IApiResponse<HomeModulesDto>> GetModulesAsync(string memberId, IReadOnlyCollection<string> disabledFeatures);
    Task<IApiResponse<object>> MarkSeenAsync(string memberId);
}

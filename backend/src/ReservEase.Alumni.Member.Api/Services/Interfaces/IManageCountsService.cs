using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IManageCountsService
{
    /// <summary>What each section of a group's management area holds, for the numbers on its tabs.</summary>
    Task<IApiResponse<ManageCountsDto>> GetAsync(string memberId, string scopeType, string scopeId);
}

using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>
/// A leader's fundraisers for their own group. Online gifts settle into the account administrators have set up (the group's own once
/// platform staff approve it, otherwise the institution's), so a leader never enters or sees any account details: manual payments stay off
/// unless an administrator turns them on and adds the details. A leader only ever sees fundraisers aimed at exactly their group.
/// </summary>
public interface IManageFundraisingService
{
    Task<IApiResponse<List<GroupCampaignDto>>> GetCampaignsAsync(string memberId, string scopeType, string scopeId);
    Task<IApiResponse<GroupCampaignDto>> CreateCampaignAsync(string memberId, string scopeType, string scopeId, SaveGroupCampaignRequest request);
    Task<IApiResponse<GroupCampaignDto>> UpdateCampaignAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupCampaignRequest request);
    Task<IApiResponse<object>> CloseCampaignAsync(string memberId, string scopeType, string scopeId, string id);
    Task<IApiResponse<List<GroupGiftDto>>> GetGiftsAsync(string memberId, string scopeType, string scopeId, string id);
}

using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IWhatsAppGroupService
{
    Task<IApiResponse<WhatsAppOverviewDto>> GetOverviewAsync();
    Task<IApiResponse<WhatsAppGroupDto>> CreateAsync(CreateWhatsAppGroupRequest request, AuthData admin);
    Task<IApiResponse<WhatsAppGroupDto>> ConfirmAsync(string groupId, AuthData admin);
    Task<IApiResponse<WhatsAppGroupDto>> UpdateAsync(string groupId, UpdateWhatsAppGroupRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteAsync(string groupId, AuthData admin);
    Task<IApiResponse<WhatsAppPublishPreviewDto>> PreviewAsync(WhatsAppPublishRequest request);
    Task<IApiResponse<object>> PublishAsync(WhatsAppPublishRequest request, AuthData admin);
    Task<IApiResponse<WhatsAppAdoptionDto>> GetAdoptionAsync();
}

using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IWebsiteService
{
    Task<IApiResponse<WebsiteEditorDto>> GetAsync();
    Task<IApiResponse<WebsiteStatusDto>> SaveDraftAsync(SaveWebsiteDraftRequest request, AuthData admin);
    Task<IApiResponse<WebsiteStatusDto>> PublishAsync(PublishWebsiteRequest request, AuthData admin);
    Task<IApiResponse<WebsiteStatusDto>> UnpublishAsync(AuthData admin);
    Task<IApiResponse<List<WebsiteVersionDto>>> GetVersionsAsync();
    Task<IApiResponse<WebsiteStatusDto>> RestoreVersionAsync(string versionId, AuthData admin);
}

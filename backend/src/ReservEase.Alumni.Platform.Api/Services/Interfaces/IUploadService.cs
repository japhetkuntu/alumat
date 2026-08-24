using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

public interface IUploadService
{
    Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file);
}

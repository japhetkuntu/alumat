using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IUploadService
{
    Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file);
    Task<IApiResponse<UploadResult>> UploadFileAsync(IFormFile file);
}

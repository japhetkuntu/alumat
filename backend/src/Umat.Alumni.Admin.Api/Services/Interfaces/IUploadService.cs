using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Services.Interfaces;

public interface IUploadService
{
    Task<IApiResponse<UploadResult>> UploadImageAsync(IFormFile file);
    Task<IApiResponse<UploadResult>> UploadFileAsync(IFormFile file);
}

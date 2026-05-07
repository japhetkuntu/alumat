using Microsoft.AspNetCore.Http;

namespace Umat.Alumni.Storage.Sdk.Services;

public interface IStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "");
    public string GetFileUrl(string fileName, string folderName = "");
    Task<List<string>> BulkUploadFilesAsync(List<IFormFile> files);

    Task<string> UploadFileAsync(Stream fileStream, string objectName, string folderName = "",
        string contentType = "application/pdf");

}

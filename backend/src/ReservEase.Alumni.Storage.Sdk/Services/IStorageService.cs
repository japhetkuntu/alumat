using Microsoft.AspNetCore.Http;

namespace ReservEase.Alumni.Storage.Sdk.Services;

public interface IStorageService
{
    /// <summary>
    /// institutionSlug, when given, is filed as a subfolder beneath folderName
    /// AND prefixed onto the saved object name — so a file is identifiable by
    /// its institution both from the storage path and from the filename alone
    /// (e.g. an individually shared/downloaded URL still reads as whose file it is).
    /// </summary>
    Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "", string institutionSlug = "");
    public string GetFileUrl(string fileName, string folderName = "", string institutionSlug = "");
    Task<List<string>> BulkUploadFilesAsync(List<IFormFile> files, string folderName = "", string institutionSlug = "");

    Task<string> UploadFileAsync(Stream fileStream, string objectName, string folderName = "",
        string institutionSlug = "", string contentType = "application/pdf");

}

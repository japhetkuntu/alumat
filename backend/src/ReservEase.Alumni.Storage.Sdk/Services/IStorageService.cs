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

    /// <summary>
    /// Stores a file nobody can fetch by URL — unlike every upload above, which is public-read so the
    /// portals can show it as an image or a link. For files holding personal data (generated reports),
    /// which must only ever leave through an authorized API call. <paramref name="key"/> is the path
    /// beneath the bucket's RootFolder; pass the same key to <see cref="OpenPrivateFileAsync"/> and
    /// <see cref="DeletePrivateFileAsync"/>.
    /// </summary>
    Task UploadPrivateFileAsync(Stream fileStream, string key, string contentType);

    /// <summary>Opens a privately stored file for reading. The caller owns, and must dispose, the stream.</summary>
    Task<Stream> OpenPrivateFileAsync(string key);

    /// <summary>Removes a privately stored file. Deleting one that is already gone is not an error.</summary>
    Task DeletePrivateFileAsync(string key);

}

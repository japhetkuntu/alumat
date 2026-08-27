using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Storage.Sdk.Options;

namespace ReservEase.Alumni.Storage.Sdk.Services
{
    public class S3StorageService : IStorageService
    {

    private readonly StorageConfig _settings;

    public S3StorageService(IOptions<StorageConfig> options)
    {
        _settings = options.Value;
    }

    private AmazonS3Client CreateClient()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,
        };
        return new AmazonS3Client(_settings.AccessKey, _settings.SecretKey, config);
    }

    /// <summary>Joins RootFolder + folderName + institutionSlug + objectName, skipping any blank segment — the slug sits as its own subfolder beneath the category folder.</summary>
    private string BuildKey(string folderName, string institutionSlug, string objectName)
    {
        var folder = string.IsNullOrEmpty(folderName) ? _settings.FolderName : folderName;
        var segments = new[] { _settings.RootFolder, folder, institutionSlug, objectName }
            .Where(s => !string.IsNullOrEmpty(s));
        return string.Join("/", segments);
    }

    /// <summary>Prefixes the slug onto the object name itself too, so the file is identifiable by institution from its filename alone, not just its storage path.</summary>
    private static string PrefixedObjectName(string objectName, string institutionSlug) =>
        string.IsNullOrEmpty(institutionSlug) ? objectName : $"{institutionSlug}-{objectName}";

    public async Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "", string institutionSlug = "")
    {
        using var client = CreateClient();
        using var stream = file.OpenReadStream();
        var prefixedObjectName = PrefixedObjectName(objectName, institutionSlug);
        var newObjectName = BuildKey(folderName, institutionSlug, prefixedObjectName);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = stream,
            Key = newObjectName,
            BucketName = _settings.BucketName,
            ContentType = file.ContentType,
            CannedACL = S3CannedACL.PublicRead,

        };

        var transferUtility = new TransferUtility(client);
        await transferUtility.UploadAsync(uploadRequest);

        return GetFileUrl(prefixedObjectName, folderName, institutionSlug);
    }

    public string GetFileUrl(string fileName, string folderName = "", string institutionSlug = "")
    {
       if( string.IsNullOrEmpty(fileName)) return string.Empty;
        var key = BuildKey(folderName, institutionSlug, fileName);
        return _settings.CdnUrlIncludesBucket
            ? $"{_settings.CdnEndpoint}/{_settings.BucketName}/{key}"
            : $"{_settings.CdnEndpoint}/{key}";
    }

    public async Task<List<string>> BulkUploadFilesAsync(List<IFormFile> files, string folderName = "", string institutionSlug = "")
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            var uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
            var url = await UploadFileAsync(file, uniqueName, folderName, institutionSlug);
            urls.Add(url);
        }
        return urls;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string objectName, string folderName = "", string institutionSlug = "", string contentType = "application/pdf")
    {
        using var client = CreateClient();
        var prefixedObjectName = PrefixedObjectName(objectName, institutionSlug);
        var newObjectName = BuildKey(folderName, institutionSlug, prefixedObjectName);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = newObjectName,
            BucketName = _settings.BucketName,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead,
        };

        var transferUtility = new TransferUtility(client);
        await transferUtility.UploadAsync(uploadRequest);

        return prefixedObjectName;
    }
}

}

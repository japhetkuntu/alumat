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

    /// <summary>Joins RootFolder + folderName + objectName, skipping any blank segment.</summary>
    private string BuildKey(string folderName, string objectName)
    {
        var folder = string.IsNullOrEmpty(folderName) ? _settings.FolderName : folderName;
        var segments = new[] { _settings.RootFolder, folder, objectName }
            .Where(s => !string.IsNullOrEmpty(s));
        return string.Join("/", segments);
    }

    public async Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "")
    {
        using var client = CreateClient();
        using var stream = file.OpenReadStream();
        var newObjectName = BuildKey(folderName, objectName);

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

        return GetFileUrl(objectName, folderName);
    }

    public string GetFileUrl(string fileName, string folderName = "")
    {
       if( string.IsNullOrEmpty(fileName)) return string.Empty;
        return $"{_settings.CdnEndpoint}/{_settings.BucketName}/{BuildKey(folderName, fileName)}";
    }

    public async Task<List<string>> BulkUploadFilesAsync(List<IFormFile> files)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            var uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
            var url = await UploadFileAsync(file, uniqueName);
            urls.Add(url);
        }
        return urls;
    }
    
    public async Task<string> UploadFileAsync(Stream fileStream, string objectName, string folderName = "", string contentType = "application/pdf")
    {
        using var client = CreateClient();
        var newObjectName = BuildKey(folderName, objectName);

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

        return objectName;
    }
}

}

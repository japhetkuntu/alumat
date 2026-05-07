using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umat.Alumni.Storage.Sdk.Options;

namespace Umat.Alumni.Storage.Sdk.Services
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

    public async Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "")
    {
        using var client = CreateClient();
        using var stream = file.OpenReadStream();
        var newFolderName = string.IsNullOrEmpty(folderName) ? _settings.FolderName : folderName;
         var newObjectName = string.IsNullOrEmpty(newFolderName)?objectName: $"{newFolderName}/{objectName}";

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
        var newFolderName = string.IsNullOrEmpty(folderName) ? _settings.FolderName : folderName;
        return $"{_settings.CdnEndpoint}/{_settings.BucketName}/{newFolderName}/{fileName}";
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
        var newFolderName = string.IsNullOrEmpty(folderName) ? _settings.FolderName : folderName;
        var newObjectName = $"{newFolderName}/{objectName}";

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

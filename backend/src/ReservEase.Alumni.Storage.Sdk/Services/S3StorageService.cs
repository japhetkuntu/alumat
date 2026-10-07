using Amazon.Runtime;
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
        _client = new Lazy<AmazonS3Client>(BuildClient, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    // One client for the life of the app. Building a client per call meant a new connection pool and a fresh TLS
    // handshake to the bucket for every single upload; the client is thread-safe and meant to be shared.
    private readonly Lazy<AmazonS3Client> _client;

    private AmazonS3Client Client => _client.Value;

    private AmazonS3Client BuildClient()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,
            // Version 4 of the AWS library adds checksum headers to every request by default. Real S3 accepts them, but
            // S3-compatible stores (DigitalOcean Spaces, MinIO of some versions) can answer with an empty-bodied error.
            // Only send them where the protocol requires one.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
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

    /// <summary>Uploaded objects get unique names and never change, so browsers and the CDN may keep images for a year.</summary>
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private static bool IsCacheable(string? contentType) =>
        contentType is not null && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public virtual async Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "", string institutionSlug = "")
    {
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
        if (IsCacheable(file.ContentType)) uploadRequest.Headers.CacheControl = ImmutableCacheControl;

        await new TransferUtility(Client).UploadAsync(uploadRequest);

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

    /// <summary>How many files of one batch go to the bucket at once. Enough to cut the wait, few enough not to swamp the connection.</summary>
    public const int BulkUploadConcurrency = 4;

    /// <summary>Uploads a batch with a few in flight at a time, returning the URLs in the same order as the files.</summary>
    public async Task<List<string>> BulkUploadFilesAsync(List<IFormFile> files, string folderName = "", string institutionSlug = "")
    {
        var urls = new string[files.Count];
        using var gate = new SemaphoreSlim(BulkUploadConcurrency);
        var tasks = files.Select(async (file, index) =>
        {
            await gate.WaitAsync();
            try
            {
                var uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
                urls[index] = await UploadFileAsync(file, uniqueName, folderName, institutionSlug);
            }
            finally { gate.Release(); }
        }).ToList();
        await Task.WhenAll(tasks);
        return urls.ToList();
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string objectName, string folderName = "", string institutionSlug = "", string contentType = "application/pdf")
    {
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
        if (IsCacheable(contentType)) uploadRequest.Headers.CacheControl = ImmutableCacheControl;

        await new TransferUtility(Client).UploadAsync(uploadRequest);

        return prefixedObjectName;
    }

    private string PrivateKey(string key) =>
        string.IsNullOrEmpty(_settings.RootFolder) ? key : $"{_settings.RootFolder}/{key}";

    public async Task UploadPrivateFileAsync(Stream fileStream, string key, string contentType)
    {
        await new TransferUtility(Client).UploadAsync(new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = PrivateKey(key),
            BucketName = _settings.BucketName,
            ContentType = contentType,
            CannedACL = S3CannedACL.Private,
        });
    }

    /// <summary>
    /// Opens a private file, wherever the process that wrote it put it. It is looked for under this process's RootFolder
    /// first, then at the bucket root, then under each top-level folder of the bucket: a worker and an API whose
    /// RootFolder settings differ (or that were deployed at different times) must never strand a finished report.
    /// </summary>
    public async Task<Stream> OpenPrivateFileAsync(string key)
    {
        AmazonS3Exception first;
        try
        {
            return await ReadObjectAsync(PrivateKey(key));
        }
        catch (AmazonS3Exception e) when (IsMissing(e))
        {
            first = e;
        }

        var tried = new HashSet<string> { PrivateKey(key) };
        if (tried.Add(key))
        {
            try { return await ReadObjectAsync(key); }
            catch (AmazonS3Exception e) when (IsMissing(e)) { }
        }

        foreach (var folder in await TopLevelFoldersAsync())
        {
            var candidate = $"{folder.TrimEnd('/')}/{key}";
            if (!tried.Add(candidate)) continue;
            try { return await ReadObjectAsync(candidate); }
            catch (AmazonS3Exception e) when (IsMissing(e)) { }
        }

        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        throw first; // unreachable; satisfies the compiler
    }

    /// <summary>The bucket's top-level "folders" (at most 50), or none when the bucket can't be listed. Separate so tests can stand in for the bucket.</summary>
    protected virtual async Task<IReadOnlyList<string>> ListTopLevelFoldersAsync()
    {
        try
        {
            var response = await Client.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request { BucketName = _settings.BucketName, Delimiter = "/", MaxKeys = 50 });
            return response.CommonPrefixes ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> TopLevelFoldersAsync() => await ListTopLevelFoldersAsync();

    private static bool IsMissing(AmazonS3Exception e) =>
        e.StatusCode == System.Net.HttpStatusCode.NotFound || e.ErrorCode is "NoSuchKey";

    /// <summary>Reads one object by its full key into a self-deleting temp file. Separate so tests can stand in for the bucket.</summary>
    protected virtual async Task<Stream> ReadObjectAsync(string fullKey)
    {
        using var response = await Client.GetObjectAsync(_settings.BucketName, fullKey);
        // Spooled to a self-deleting temp file: the S3 response stream dies with the client above,
        // and a report can be too large to hold in memory for the length of a slow download.
        var spool = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await response.ResponseStream.CopyToAsync(spool);
        spool.Position = 0;
        return spool;
    }

    public async Task DeletePrivateFileAsync(string key)
    {
        await Client.DeleteObjectAsync(_settings.BucketName, PrivateKey(key));
    }
}

}

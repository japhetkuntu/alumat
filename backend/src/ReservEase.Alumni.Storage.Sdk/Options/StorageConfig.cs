namespace ReservEase.Alumni.Storage.Sdk.Options;

public class StorageConfig
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string CdnEndpoint { get; set; } = string.Empty;
    public string FolderName { get; set; } = "alumni";

    /// <summary>
    /// Top-level prefix for every object this app writes — for when
    /// <see cref="BucketName"/> is shared with other, unrelated projects (e.g.
    /// one DigitalOcean Spaces bucket reused across several apps). Every key
    /// becomes "{RootFolder}/{FolderName}/{objectName}" instead of just
    /// "{FolderName}/{objectName}", so nothing collides with whatever else
    /// lives in the same bucket. Leave blank if the bucket is dedicated to
    /// this app only — existing keys are unaffected either way.
    /// </summary>
    public string RootFolder { get; set; } = string.Empty;
}

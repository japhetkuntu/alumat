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

    /// <summary>
    /// Whether <see cref="CdnEndpoint"/> is bucket-agnostic and needs
    /// <see cref="BucketName"/> inserted into the URL path (path-style, e.g.
    /// self-hosted MinIO: "https://cdn.example.com/{bucket}/{key}") vs.
    /// already bucket-specific and must NOT have it repeated (virtual-hosted
    /// style, e.g. a DigitalOcean Spaces CDN endpoint:
    /// "https://{bucket}.fra1.cdn.digitaloceanspaces.com/{key}" — the bucket
    /// is already the subdomain, so adding it again in the path 404s).
    /// Defaults to true (path-style) to match this project's original
    /// MinIO-backed deployment; set false for DigitalOcean Spaces.
    /// </summary>
    public bool CdnUrlIncludesBucket { get; set; } = true;
}

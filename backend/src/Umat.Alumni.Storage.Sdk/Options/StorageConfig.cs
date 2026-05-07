namespace Umat.Alumni.Storage.Sdk.Options;

public class StorageConfig
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string CdnEndpoint { get; set; } = string.Empty;
    public string FolderName { get; set; } = "alumni";
}

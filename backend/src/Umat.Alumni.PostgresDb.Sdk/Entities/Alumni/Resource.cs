namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Resource : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? UploadedBy { get; set; }
    public int DownloadCount { get; set; } = 0;
    public List<int>? YearGroups { get; set; }
}

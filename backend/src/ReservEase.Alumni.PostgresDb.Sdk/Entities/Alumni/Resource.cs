using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Resource : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Null = institution-wide resource (unchanged, pre-existing behavior). Set = belongs to one Community; only its approved members/leaders can see it.</summary>
    public string? CommunityId { get; set; }

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

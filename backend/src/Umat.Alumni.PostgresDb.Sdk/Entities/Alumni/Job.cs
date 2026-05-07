namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Job : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ApplyUrl { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = "Active";
    public string PostedBy { get; set; } = string.Empty;
    public string? BannerImageUrl { get; set; }
    public List<int>? YearGroups { get; set; }
}

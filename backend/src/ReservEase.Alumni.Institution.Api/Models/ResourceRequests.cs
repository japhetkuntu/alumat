namespace ReservEase.Alumni.Institution.Api.Models;

public class CreateResourceRequest
{
    /// <summary>Optional — scopes this resource to one Community instead of the whole institution.</summary>
    public string? CommunityId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ExternalUrl { get; set; }
    public IFormFile? File { get; set; }
    public IFormFile? BannerImage { get; set; }
    public List<int>? YearGroups { get; set; }
}

public class UpdateResourceRequest
{
    public string ResourceId { get; set; } = "";
    public string? CommunityId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ExternalUrl { get; set; }
    public IFormFile? File { get; set; }
    public IFormFile? BannerImage { get; set; }
    public List<int>? YearGroups { get; set; }
}

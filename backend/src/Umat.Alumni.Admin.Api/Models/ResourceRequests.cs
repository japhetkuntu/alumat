namespace Umat.Alumni.Admin.Api.Models;

public class CreateResourceRequest
{
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
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ExternalUrl { get; set; }
    public IFormFile? File { get; set; }
    public IFormFile? BannerImage { get; set; }
    public List<int>? YearGroups { get; set; }
}

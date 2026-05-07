namespace Umat.Alumni.Admin.Api.Models;

public class CreateJobRequest
{
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Description { get; set; }
    public string? ApplyUrl { get; set; }
    public DateTime? Deadline { get; set; }
    public List<int>? YearGroups { get; set; }
    public IFormFile? BannerImage { get; set; }
}

public class UpdateJobRequest
{
    public string JobId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Description { get; set; }
    public string? ApplyUrl { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = "";
    public List<int>? YearGroups { get; set; }
    public IFormFile? BannerImage { get; set; }
}

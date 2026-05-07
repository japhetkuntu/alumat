namespace Umat.Alumni.Admin.Api.Models;

public class CreateNewsPostRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsPinned { get; set; }
    public string Status { get; set; } = "Draft";
    public List<IFormFile>? Images { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
    public List<int>? YearGroups { get; set; }
}

public class UpdateNewsPostRequest
{
    public string PostId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsPinned { get; set; }
    public string Status { get; set; } = "Draft";
    public List<IFormFile>? Images { get; set; }
    public List<string>? ExistingImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
    public List<int>? YearGroups { get; set; }
}

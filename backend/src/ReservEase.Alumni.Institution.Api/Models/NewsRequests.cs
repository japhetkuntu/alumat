namespace ReservEase.Alumni.Institution.Api.Models;

public class CreateNewsPostRequest
{
    /// <summary>Optional — scopes this post to one Community instead of the whole institution.</summary>
    public string? CommunityId { get; set; }
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
    public string? CommunityId { get; set; }
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

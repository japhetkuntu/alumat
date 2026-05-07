namespace Umat.Alumni.Admin.Api.Models;

public class CreateEventRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Venue { get; set; } = "";
    public int? Capacity { get; set; }
    public bool IsTicketed { get; set; }
    public decimal? TicketPrice { get; set; }
    public string? GoogleLocationUrl { get; set; }
    public List<int>? YearGroups { get; set; }
    public IFormFile? BannerImage { get; set; }
    public List<IFormFile>? Images { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
}

public class UpdateEventRequest
{
    public string EventId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Venue { get; set; } = "";
    public int? Capacity { get; set; }
    public bool IsTicketed { get; set; }
    public decimal? TicketPrice { get; set; }
    public string? GoogleLocationUrl { get; set; }
    public string Status { get; set; } = "";
    public List<int>? YearGroups { get; set; }
    public IFormFile? BannerImage { get; set; }
    public List<IFormFile>? Images { get; set; }
    public List<string>? ExistingImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
}

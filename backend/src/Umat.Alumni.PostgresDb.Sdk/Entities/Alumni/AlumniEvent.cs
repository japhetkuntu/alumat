namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class AlumniEvent : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public int RsvpCount { get; set; }
    public bool IsTicketed { get; set; }
    public decimal? TicketPrice { get; set; }
    public string Status { get; set; } = "Upcoming"; // Upcoming, Ongoing, Completed, Cancelled
    public List<int>? YearGroups { get; set; }
    public string? GoogleLocationUrl { get; set; }
    public string? BannerImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }
}

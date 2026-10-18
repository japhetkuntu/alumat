using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Models;

public class CreateEventRequest
{
    /// <summary>Optional — scopes this event to one Community instead of the whole institution.</summary>
    public string? CommunityId { get; set; }
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
    public string? CommunityId { get; set; }
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

/// <summary>Whether this community may charge for event tickets, and whether its payout account is ready.</summary>
public record EventTicketingDto(bool PaidEventsEnabled, bool PayoutReady);

/// <summary>The updates of one event in the order the organiser wants them shown (pinned ones stay above the rest).</summary>
public class EventUpdateOrderRequest
{
    public List<string> Ids { get; set; } = [];
}

/// <summary>An event update as saved: its title, category and parts. New files travel beside this, named by the id of the part they belong to.</summary>
public class EventUpdateSaveRequest
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public List<EventUpdateBlock> Blocks { get; set; } = [];
}

using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class AlumniEvent : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Null = institution-wide event (unchanged, pre-existing behavior). Set = belongs to one Community; only its approved members/leaders can see or RSVP to it.</summary>
    public string? CommunityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public int RsvpCount { get; set; }
    /// <summary>
    /// Set by the organiser to stop taking new RSVPs or ticket sales while the event stays up. Seats already taken stay valid and free RSVPs
    /// can still be cancelled. It is the organiser's way to stop a paid event's sales, since a paid event can't be cancelled.
    /// </summary>
    public bool RegistrationClosed { get; set; }
    public bool IsTicketed { get; set; }
    public decimal? TicketPrice { get; set; }
    public string Status { get; set; } = "Upcoming"; // Upcoming, Ongoing, Completed, Cancelled
    public List<int>? YearGroups { get; set; }
    public string? GoogleLocationUrl { get; set; }
    public string? BannerImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<string>? YoutubeVideoUrls { get; set; }

    /// <summary>Set once the day-before RSVP reminder has gone out (see ScheduledJobsActivities.SendDueEventRemindersForInstitutionAsync), so a schedule that fires more than once a day never double-sends it.</summary>
    public DateTime? DayBeforeReminderSentAt { get; set; }
}

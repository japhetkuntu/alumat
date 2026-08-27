using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class MentorProfile : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;
    public MemberSnapshot? Member { get; set; }
    public string Area { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public int MaxMentees { get; set; }
    public int CurrentMenteeCount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Paused
    public List<int>? YearGroups { get; set; }

    // Contact info the mentor chooses to share — deliberately separate from
    // Member.Phone/LinkedInUrl (a mentor may want different contact channels
    // for mentorship than what's on their general profile). Only ever
    // exposed to a mentee once their specific request is Accepted — see
    // EntityDtoExtensions' ToDto(includeContact:) overloads.
    public string? ContactLinkedInUrl { get; set; }
    public string? ContactWhatsAppNumber { get; set; }
    public string? ContactPhoneNumber { get; set; }
}

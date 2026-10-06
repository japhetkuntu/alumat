using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Referral : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string ReferrerId { get; set; } = string.Empty;
    public MemberSnapshot? Referrer { get; set; }
    public string ReferredEmail { get; set; } = string.Empty;
    public string? ReferredMemberId { get; set; }
    public MemberSnapshot? ReferredMember { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Registered, MembershipPaid

    /// <summary>Set when the member joined through a community invitation, so growth can be read per community.</summary>
    public string? CommunityId { get; set; }
    /// <summary>Where the invitation was shared, e.g. "whatsapp" or "link". Free text from a short allow-list; never trusted for anything but reporting.</summary>
    public string? Channel { get; set; }
}

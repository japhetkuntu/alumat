namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A registration link issued to one student ID when a partner's status check finds no such member. Opening it lets that one
/// student register and be approved straight away. A newer link for the same ID replaces the older one, and a link works once.
/// Only a hash of the token is stored.
/// </summary>
public class OnboardingLink : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>The student ID the link was issued for, trimmed. The member created from it gets exactly this ID.</summary>
    public string StudentId { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when a newer link was issued for the same student ID.</summary>
    public DateTime? SupersededAt { get; set; }

    /// <summary>Set, together with <see cref="MemberId"/>, when the registration is completed.</summary>
    public DateTime? ConsumedAt { get; set; }
    public string? MemberId { get; set; }
}

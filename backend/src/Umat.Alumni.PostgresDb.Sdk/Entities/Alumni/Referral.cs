namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Referral : BaseEntity
{
    public string ReferrerId { get; set; } = string.Empty;
    public MemberSnapshot? Referrer { get; set; }
    public string ReferredEmail { get; set; } = string.Empty;
    public string? ReferredMemberId { get; set; }
    public MemberSnapshot? ReferredMember { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Registered, MembershipPaid
}

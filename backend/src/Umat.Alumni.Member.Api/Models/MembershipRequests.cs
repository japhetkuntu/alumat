namespace Umat.Alumni.Member.Api.Models;

public record InitiateMembershipRenewalRequest(string CampaignId, int Years, string PaymentMethod = "online", string? CallbackUrl = null);

public record MembershipStatusResponse(
    bool IsMembershipActive,
    DateTime? MembershipExpiry,
    int MembershipYearsPaid,
    DateTime? LastMembershipPaidAt,
    bool IsCurrentYearPaid,
    bool HasArrears,
    int ArrearsCount,
    List<int> ArrearsYears)
{
    // Backwards-compat constructor used in fallback path (no campaign data)
    public MembershipStatusResponse(bool isMembershipActive, DateTime? membershipExpiry, int membershipYearsPaid, DateTime? lastMembershipPaidAt)
        : this(isMembershipActive, membershipExpiry, membershipYearsPaid, lastMembershipPaidAt,
               isMembershipActive, false, 0, []) { }
}

namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// What a membership-dues campaign is aiming to collect: the fee for every eligible member. It is worked out from the
/// current fee and head count each time rather than kept as a number typed once, so changing the fee (or members joining)
/// moves the target and the progress bar with it. At least one member is counted so an empty list never gives zero.
/// </summary>
public static class MembershipTarget
{
    public static decimal For(decimal feePerMember, int eligibleMembers) => feePerMember * Math.Max(eligibleMembers, 1);
}

using ReservEase.Alumni.Common.Sdk.Extensions;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>A dues campaign aims to collect the fee from every eligible member, so a new fee has to move the target.</summary>
public class MembershipTargetTests
{
    [Theory]
    [InlineData(150, 5, 750)]
    [InlineData(200, 5, 1000)]
    [InlineData(200, 0, 200)]   // an empty list never gives a zero target
    [InlineData(99.5, 10, 995)]
    public void TargetIsTheFeeForEveryEligibleMember(double fee, int members, double expected)
        => Assert.Equal((decimal)expected, MembershipTarget.For((decimal)fee, members));
}

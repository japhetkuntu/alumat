using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Xunit;

namespace ReservEase.Alumni.Notifications.Tests;

public class PledgeProgressTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Pledge MakePledge(decimal amount = 500m, int dueInDays = 10, string status = PledgeStatuses.Open) => new()
    {
        Amount = amount,
        DueDate = Now.Date.AddDays(dueInDays),
        Status = status,
        CreatedAt = Now.AddDays(-5),
    };

    private static Contribution Paid(decimal amount, int daysAgo = 1, string status = "Successful") => new()
    {
        Amount = amount,
        Status = status,
        CreatedAt = Now.AddDays(-daysAgo),
    };

    [Fact]
    public void NoPayments_IsPledged_WithFullAmountOutstanding()
    {
        var r = PledgeProgress.Compute(MakePledge(), [], Now);
        Assert.Equal(PledgeStates.Pledged, r.State);
        Assert.Equal(0m, r.Paid);
        Assert.Equal(500m, r.Outstanding);
    }

    [Fact]
    public void PartialPayment_IsPartPaid_AndLeavesTheRestOutstanding()
    {
        var r = PledgeProgress.Compute(MakePledge(), [Paid(200m)], Now);
        Assert.Equal(PledgeStates.PartPaid, r.State);
        Assert.Equal(200m, r.Paid);
        Assert.Equal(300m, r.Outstanding);
    }

    [Fact]
    public void PaymentsAddingUpToTheAmount_AreFulfilled_EvenAcrossSeveralPayments()
    {
        var r = PledgeProgress.Compute(MakePledge(), [Paid(200m), Paid(300m)], Now);
        Assert.Equal(PledgeStates.Fulfilled, r.State);
        Assert.Equal(0m, r.Outstanding);
    }

    [Fact]
    public void OverPaying_DoesNotGoNegative()
    {
        var r = PledgeProgress.Compute(MakePledge(), [Paid(800m)], Now);
        Assert.Equal(PledgeStates.Fulfilled, r.State);
        Assert.Equal(0m, r.Outstanding);
    }

    [Fact]
    public void OnlySuccessfulPayments_MadeAfterThePledge_Count()
    {
        var r = PledgeProgress.Compute(MakePledge(), [
            Paid(100m, status: "Pending"),
            Paid(100m, status: "Rejected"),
            Paid(400m, daysAgo: 30), // before the pledge existed
        ], Now);
        Assert.Equal(0m, r.Paid);
        Assert.Equal(PledgeStates.Pledged, r.State);
    }

    [Fact]
    public void PastDueAndUnpaid_IsOverdue()
    {
        var r = PledgeProgress.Compute(MakePledge(dueInDays: -2), [Paid(100m)], Now);
        Assert.Equal(PledgeStates.Overdue, r.State);
        Assert.Equal(400m, r.Outstanding);
    }

    [Fact]
    public void PastDueButPaidInFull_IsFulfilledNotOverdue()
    {
        Assert.Equal(PledgeStates.Fulfilled, PledgeProgress.Compute(MakePledge(dueInDays: -2), [Paid(500m)], Now).State);
    }

    [Theory]
    [InlineData(PledgeStatuses.Cancelled, PledgeStates.Cancelled)]
    [InlineData(PledgeStatuses.WrittenOff, PledgeStates.WrittenOff)]
    public void CancelledOrWrittenOff_StaysThatWayRegardlessOfPayments(string status, string expected)
    {
        Assert.Equal(expected, PledgeProgress.Compute(MakePledge(status: status), [Paid(500m)], Now).State);
    }

    [Fact]
    public void ReminderWording_IsGentle_AndNamesTheAmountAndFundraiser()
    {
        foreach (var stage in new[] { PledgeReminderMessages.Upcoming, PledgeReminderMessages.Due, PledgeReminderMessages.Overdue })
        {
            var (title, body, action) = PledgeReminderMessages.Build(stage, "Library Fund", 300m, Now);
            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.Contains("Library Fund", body);
            Assert.Contains("GHS 300.00", body);
            Assert.False(string.IsNullOrWhiteSpace(action));
        }
    }
}

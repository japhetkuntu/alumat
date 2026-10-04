using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using Xunit;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class ActivationTargetMathTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due = Start.AddDays(100);

    private static ActivationTarget Level(decimal baseline, decimal goal, string status = TargetStatuses.Active) => new()
    {
        Metric = TargetMetrics.LiveInstitutions, BaselineValue = baseline, GoalValue = goal, StartDate = Start, DueDate = Due, Status = status,
    };

    private static ActivationTarget Flow(decimal goal) => new()
    {
        Metric = TargetMetrics.OnboardingLeads, BaselineValue = 0, GoalValue = goal, StartDate = Start, DueDate = Due, Status = TargetStatuses.Active,
    };

    [Fact]
    public void LevelMetric_Progress_IsMeasuredFromWhereTheTeamStarted()
    {
        // Started at 4 live institutions, aiming for 20: being at 12 is halfway, not 60%.
        var r = ActivationTargetMath.Compute(Level(4, 20), 12, Start.AddDays(50));
        Assert.Equal(50, r.Percent);
        Assert.Equal(4m, r.Baseline);
    }

    [Fact]
    public void FlowMetric_CountsFromZero_RegardlessOfBaseline()
    {
        var t = Flow(50);
        t.BaselineValue = 999; // ignored for a flow metric
        var r = ActivationTargetMath.Compute(t, 10, Start.AddDays(50));
        Assert.Equal(20, r.Percent);
        Assert.Equal(0m, r.Baseline);
    }

    [Fact]
    public void ExpectedProgress_IsAStraightLineFromStartToDueDate()
    {
        var r = ActivationTargetMath.Compute(Level(0, 100), 0, Start.AddDays(25));
        Assert.Equal(25m, r.Expected);
        Assert.Equal(0m, ActivationTargetMath.Compute(Level(0, 100), 0, Start).Expected);
        Assert.Equal(100m, ActivationTargetMath.Compute(Level(0, 100), 0, Due).Expected);
        Assert.Equal(100m, ActivationTargetMath.Compute(Level(0, 100), 0, Due.AddDays(30)).Expected); // never beyond the goal
    }

    [Fact]
    public void OnPace_IsOnTrack_AndBehindWhenItFallsShort()
    {
        var day50 = Start.AddDays(50); // expected 50 of 100
        Assert.Equal("OnTrack", ActivationTargetMath.Compute(Level(0, 100), 50, day50).Health);
        Assert.Equal("OnTrack", ActivationTargetMath.Compute(Level(0, 100), 80, day50).Health);
        Assert.Equal("Behind", ActivationTargetMath.Compute(Level(0, 100), 30, day50).Health);
    }

    [Fact]
    public void AFewPercentUnderPace_IsStillOnTrack()
    {
        // 5% grace: 46 against an expected 50 is within it, 44 is not.
        var day50 = Start.AddDays(50);
        Assert.Equal("OnTrack", ActivationTargetMath.Compute(Level(0, 100), 46, day50).Health);
        Assert.Equal("Behind", ActivationTargetMath.Compute(Level(0, 100), 44, day50).Health);
    }

    [Fact]
    public void ReachingTheGoal_IsAchieved_AndPercentStopsAt100()
    {
        var r = ActivationTargetMath.Compute(Level(0, 100), 130, Start.AddDays(10));
        Assert.Equal("Achieved", r.Health);
        Assert.Equal(100, r.Percent);
    }

    [Fact]
    public void ANewTarget_IsOnTrack_BecauseNothingIsExpectedYet()
    {
        Assert.Equal("OnTrack", ActivationTargetMath.Compute(Level(6, 20), 6, Start).Health);
    }

    [Theory]
    [InlineData(TargetStatuses.Cancelled, "Cancelled")]
    [InlineData(TargetStatuses.Achieved, "Achieved")]
    [InlineData(TargetStatuses.Missed, "Missed")]
    public void AnEndedTarget_KeepsItsOutcome_WhateverTheNumbersDoNow(string status, string expected)
    {
        Assert.Equal(expected, ActivationTargetMath.Compute(Level(0, 100, status), 3, Start.AddDays(90)).Health);
    }

    [Fact]
    public void ADegenerateSpan_DoesNotDivideByZero()
    {
        // Goal equal to the baseline (can't be created, but must not crash if data says so).
        var r = ActivationTargetMath.Compute(Level(10, 10), 10, Start.AddDays(10));
        Assert.Equal(100, r.Percent);
    }

    [Fact]
    public void FlowMetrics_AreIdentifiedCorrectly()
    {
        Assert.True(TargetMetrics.IsFlow(TargetMetrics.OnboardingLeads));
        Assert.True(TargetMetrics.IsFlow(TargetMetrics.PaymentVolume));
        Assert.False(TargetMetrics.IsFlow(TargetMetrics.LiveInstitutions));
        Assert.False(TargetMetrics.IsFlow(TargetMetrics.Custom));
    }
}

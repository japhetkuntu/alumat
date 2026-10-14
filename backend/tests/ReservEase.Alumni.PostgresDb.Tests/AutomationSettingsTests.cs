using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class AutomationSettingsTests
{
    [Fact]
    public void The_defaults_are_what_every_community_had_before_this_was_configurable()
    {
        var d = new AutomationSettings();
        Assert.True(d.DuesReminders && d.EventReminders && d.PledgeReminders && d.ReengageQuietMembers);
        Assert.Equal(21, d.DuesReminderEveryDays);   // the old fixed cooldown
        Assert.Equal(30, d.QuietDays);               // the old fixed quiet period (AutomationLimits.MemberQuietDays)
        Assert.Equal(0, d.DuesChaseAfterDays);       // chase straight away, as before
        Assert.Equal(new AutomationLimits().MemberQuietDays, d.QuietDays);
    }

    [Fact]
    public void Numbers_are_brought_back_into_their_allowed_range()
    {
        var wild = new AutomationSettings { DuesReminderEveryDays = 0, DuesChaseAfterDays = 9999, QuietDays = 1 }.Normalised();
        Assert.Equal(AutomationSettings.MinReminderEveryDays, wild.DuesReminderEveryDays);
        Assert.Equal(AutomationSettings.MaxChaseAfterDays, wild.DuesChaseAfterDays);
        Assert.Equal(AutomationSettings.MinQuietDays, wild.QuietDays);

        var huge = new AutomationSettings { DuesReminderEveryDays = 5000, QuietDays = 5000 }.Normalised();
        Assert.Equal(AutomationSettings.MaxReminderEveryDays, huge.DuesReminderEveryDays);
        Assert.Equal(AutomationSettings.MaxQuietDays, huge.QuietDays);
    }

    [Fact]
    public void A_communitys_own_quiet_period_changes_who_counts_as_quiet()
    {
        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var lastSeen = now.AddDays(-20);
        var joined = now.AddDays(-400);

        // Quiet after 30 days: seen 20 days ago, so still around.
        Assert.False(AutomationPolicy.ShouldReengage(false, true, true, joined, lastSeen, false, null, null, 2, now, new AutomationLimits { MemberQuietDays = 30 }));
        // This community considers 14 days quiet: the same member is now eligible.
        Assert.True(AutomationPolicy.ShouldReengage(false, true, true, joined, lastSeen, false, null, null, 2, now, new AutomationLimits { MemberQuietDays = 14 }));
    }
}

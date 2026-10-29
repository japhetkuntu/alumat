using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>The rules every portal shares for a poll: what state it is in, who is in its audience, and when members may see the figures.</summary>
public class PollRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Opens = Now.AddDays(-1);
    private static readonly DateTime Closes = Now.AddDays(2);

    [Fact]
    public void A_poll_is_a_draft_until_published_then_scheduled_open_or_closed_by_its_dates()
    {
        Assert.Equal("Draft", PollRules.StateOf(PollStatuses.Draft, Opens, Closes, Now));
        Assert.Equal("Scheduled", PollRules.StateOf(PollStatuses.Published, Now.AddHours(1), Closes, Now));
        Assert.Equal("Open", PollRules.StateOf(PollStatuses.Published, Opens, Closes, Now));
        Assert.Equal("Closed", PollRules.StateOf(PollStatuses.Published, Opens, Now, Now));          // closes at the instant, not after
        Assert.Equal("Closed", PollRules.StateOf(PollStatuses.Published, Opens, Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void Results_follow_the_mode_live_while_open_after_closing_or_only_once_the_admin_publishes()
    {
        bool Visible(string mode, DateTime closes, DateTime? published = null) => PollRules.ResultsVisible(PollStatuses.Published, mode, Opens, closes, published, Now);
        var past = Now.AddMinutes(-5);

        Assert.True(Visible(PollResultsModes.Live, Closes));
        Assert.False(Visible(PollResultsModes.AfterClose, Closes));
        Assert.True(Visible(PollResultsModes.AfterClose, past));
        Assert.False(Visible(PollResultsModes.Manual, past));                    // closed, but the admin has not published
        Assert.True(Visible(PollResultsModes.Manual, past, Now));
        Assert.False(PollRules.ResultsVisible(PollStatuses.Draft, PollResultsModes.Live, Opens, Closes, null, Now));          // never for a draft
        Assert.False(PollRules.ResultsVisible(PollStatuses.Published, PollResultsModes.Live, Now.AddHours(1), Closes, null, Now));   // not before it opens
    }

    [Fact]
    public void The_audience_is_everyone_a_set_of_year_groups_or_a_groups_approved_members_and_a_group_wins()
    {
        Assert.True(PollRules.InAudience(null, null, 2015, false));
        Assert.True(PollRules.InAudience([], null, 2015, false));
        Assert.True(PollRules.InAudience([2015, 2016], null, 2016, false));
        Assert.False(PollRules.InAudience([2015, 2016], null, 2020, false));
        Assert.True(PollRules.InAudience(null, "g1", 1999, true));
        Assert.False(PollRules.InAudience(null, "g1", 2015, false));
        Assert.False(PollRules.InAudience([2015], "g1", 2015, false));          // a group takes precedence over year groups
    }

    [Fact]
    public void A_position_with_one_aspirant_is_a_yes_or_no_question()
    {
        Assert.True(PollRules.IsYesNo(1));
        Assert.False(PollRules.IsYesNo(2));
        Assert.False(PollRules.IsYesNo(0));
    }
}

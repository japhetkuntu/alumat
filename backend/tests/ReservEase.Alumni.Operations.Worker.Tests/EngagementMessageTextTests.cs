using ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public class EngagementMessageTextTests
{
    [Fact]
    public void A_quiet_member_note_lists_what_is_happening_and_says_how_to_switch_it_off()
    {
        var (title, body) = EngagementMessageText.QuietMember("UMaT Alumni", ["Coming up: Homecoming, 14 Oct", "New opportunity: Engineer at Gold Fields"]);

        Assert.Equal("Something is happening at UMaT Alumni", title);
        Assert.Contains("Coming up: Homecoming, 14 Oct. New opportunity: Engineer at Gold Fields.", body);
        Assert.Contains("switch these notes off", body);
    }

    [Fact]
    public void Titles_typed_by_administrators_cannot_inject_markup_into_the_email()
    {
        var (title, body) = EngagementMessageText.QuietMember("A & B <Alumni>", ["Coming up: <script>alert(1)</script> Night, 1 Oct"]);

        Assert.DoesNotContain("<script>", body);
        Assert.DoesNotContain("<Alumni>", title + body);
        Assert.Contains("&lt;script&gt;", body);
        Assert.Contains("A &amp; B &lt;Alumni&gt;", title);
    }

    [Fact]
    public void No_more_than_three_items_are_ever_listed()
        => Assert.DoesNotContain("Item 4", EngagementMessageText.QuietMember("X", ["Item 1", "Item 2", "Item 3", "Item 4"]).Body);

    [Theory]
    [InlineData(1, 0, "There is 1 suggested action.")]
    [InlineData(3, 0, "There are 3 suggested actions.")]
    [InlineData(0, 1, "There is 1 member waiting for approval.")]
    [InlineData(2, 4, "There are 2 suggested actions and 4 members waiting for approval.")]
    public void The_reminder_counts_what_is_waiting_in_correct_english(int suggestions, int pending, string expected)
        => Assert.Contains(expected, EngagementMessageText.AdminReminder("X", 20, suggestions, pending).Body);

    [Fact]
    public void The_reminder_says_how_long_and_does_not_invent_a_number_when_they_have_never_signed_in()
    {
        Assert.Contains("It has been 20 days since you last visited", EngagementMessageText.AdminReminder("X", 20, 1, 0).Body);
        Assert.Contains("It has been a while since you last visited", EngagementMessageText.AdminReminder("X", null, 1, 0).Body);
    }

    [Fact]
    public void The_escalation_names_the_colleague_reassures_that_nothing_changed_and_encodes_their_name()
    {
        var (title, body) = EngagementMessageText.AdminEscalation("X", "Kojo <b>Colleague</b>", 45);

        Assert.Contains("has been away", title);
        Assert.Contains("45 days", body);
        Assert.Contains("Nothing has been changed on their account", body);
        Assert.DoesNotContain("<b>", title + body);
    }

    [Fact]
    public void An_unknown_colleague_is_described_neutrally_not_as_an_empty_name()
        => Assert.StartsWith("A fellow administrator has been away", EngagementMessageText.AdminEscalation("X", "", 40).Title.Split(": ")[1]);
}

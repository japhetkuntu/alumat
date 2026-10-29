using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>In its last day a poll reminds the members who have not voted, once, and nobody else.</summary>
public class PollClosingReminderTests
{
    private const string Inst = "inst-1";

    private static MemberEntity Person(string id) => new() { Id = id, FirstName = id, LastName = "X", Email = $"{id}@x.org", Status = "Active", GraduationYear = 2015, InstitutionId = Inst };
    private static Poll Closing(string id, DateTime closes, string status = PollStatuses.Published) =>
        new() { Id = id, Title = "Elections", Status = status, InstitutionId = Inst, OpensAt = DateTime.UtcNow.AddDays(-2), ClosesAt = closes };

    private static ScheduledJobsRig Rig(Institution inst, params object[] seed)
    {
        var rig = new ScheduledJobsRig();
        rig.Seed(inst).GetAwaiter().GetResult();
        rig.Seed(seed).GetAwaiter().GetResult();
        return rig;
    }

    private static Institution Active() => new() { Id = Inst, Name = "Demo", Slug = "demo", Status = "Active" };

    [Fact]
    public async Task Members_who_have_not_voted_are_reminded_once_and_those_who_have_are_left_alone()
    {
        var rig = Rig(Active(), Person("voted"), Person("not1"), Person("not2"), Closing("p1", DateTime.UtcNow.AddHours(5)),
            new PollParticipation { PollId = "p1", MemberId = "voted", InstitutionId = Inst });

        Assert.Equal(2, await rig.Activities.SendPollClosingRemindersForInstitutionAsync(Inst));
        Assert.Equal(0, await rig.Activities.SendPollClosingRemindersForInstitutionAsync(Inst));      // never twice

        var told = await rig.Db().Notifications.IgnoreQueryFilters().Where(n => n.Type == "PollClosing").Select(n => n.RecipientId).ToListAsync();
        Assert.Equal(["not1", "not2"], told.OrderBy(x => x));
    }

    [Fact]
    public async Task Only_polls_closing_within_a_day_that_are_published_get_a_reminder()
    {
        var rig = Rig(Active(), Person("a"),
            Closing("far", DateTime.UtcNow.AddDays(5)), Closing("draft", DateTime.UtcNow.AddHours(5), PollStatuses.Draft), Closing("over", DateTime.UtcNow.AddHours(-1)));
        Assert.Equal(0, await rig.Activities.SendPollClosingRemindersForInstitutionAsync(Inst));
    }

    [Fact]
    public async Task An_institution_without_the_polls_feature_is_never_reminded()
    {
        var inst = Active(); inst.DisabledFeatures = [InstitutionFeatures.Polls];
        var rig = Rig(inst, Person("a"), Closing("p1", DateTime.UtcNow.AddHours(5)));
        Assert.Equal(0, await rig.Activities.SendPollClosingRemindersForInstitutionAsync(Inst));
    }
}

using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>
/// A request to connect that has gone unanswered is worth one reminder, because the person it is for may simply have stopped visiting.
/// Notices are not delivered here (Temporal is off); a reminder is counted and the request is marked.
/// </summary>
public class IntroductionReminderTests
{
    private const string Inst = "inst-1";
    private static readonly DateTime Now = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private static MemberEntity Person(string id, bool takes = true, string status = "Active") => new()
    {
        Id = id, FirstName = id, LastName = "X", Email = $"{id}@x.org", Status = status, InstitutionId = Inst, AllowIntroductions = takes,
    };

    private static Introduction Ask(string id, int daysAgo, string status = "Pending", DateTime? reminded = null) => new()
    {
        Id = id, InstitutionId = Inst, RequesterId = "asker", RecipientId = "them", Reason = "x", Status = status,
        CreatedAt = Now.AddDays(-daysAgo), ReminderSentAt = reminded,
    };

    private static ScheduledJobsRig Rig(Institution? institution, params object[] seed)
    {
        var rig = new ScheduledJobsRig();
        rig.Seed(institution ?? new Institution { Id = Inst, Name = "Demo", Slug = "demo", Status = "Active" }).GetAwaiter().GetResult();
        rig.Seed(seed).GetAwaiter().GetResult();
        return rig;
    }

    private static async Task<DateTime?> Marked(ScheduledJobsRig rig, string id)
    {
        using var db = rig.Db();
        return (await db.Set<Introduction>().IgnoreQueryFilters().SingleAsync(i => i.Id == id)).ReminderSentAt;
    }

    [Fact]
    public async Task A_request_unanswered_for_three_days_gets_one_reminder_and_is_marked()
    {
        var rig = Rig(null, Person("asker"), Person("them"), Ask("a", daysAgo: 4));
        Assert.Equal(1, await rig.Activities.SendIntroductionRemindersAsync(Now));
        Assert.Equal(Now, await Marked(rig, "a"));
    }

    [Fact]
    public async Task It_is_never_sent_twice()
    {
        var rig = Rig(null, Person("asker"), Person("them"), Ask("a", daysAgo: 4));
        Assert.Equal(1, await rig.Activities.SendIntroductionRemindersAsync(Now));
        Assert.Equal(0, await rig.Activities.SendIntroductionRemindersAsync(Now.AddDays(1)));
    }

    [Fact]
    public async Task A_request_that_is_too_new_too_old_already_reminded_or_answered_is_left_alone()
    {
        var rig = Rig(null, Person("asker"), Person("them"),
            Ask("new", daysAgo: 1), Ask("old", daysAgo: 20), Ask("done", daysAgo: 4, reminded: Now.AddDays(-1)), Ask("accepted", daysAgo: 4, status: "Accepted"));
        Assert.Equal(0, await rig.Activities.SendIntroductionRemindersAsync(Now));
        Assert.Null(await Marked(rig, "new"));
        Assert.Null(await Marked(rig, "old"));
    }

    [Fact]
    public async Task Nothing_is_sent_to_someone_who_no_longer_takes_introductions_or_is_not_active_but_the_request_is_not_looked_at_again()
    {
        var rig = Rig(null, Person("asker"), Person("them", takes: false), Ask("a", daysAgo: 4));
        Assert.Equal(0, await rig.Activities.SendIntroductionRemindersAsync(Now));
        Assert.NotNull(await Marked(rig, "a"));

        var inactive = Rig(null, Person("asker"), Person("them", status: "Banned"), Ask("b", daysAgo: 4));
        Assert.Equal(0, await inactive.Activities.SendIntroductionRemindersAsync(Now));
    }

    [Fact]
    public async Task Nothing_is_sent_when_either_side_has_blocked_the_other()
    {
        var rig = Rig(null, Person("asker"), Person("them"), Ask("a", daysAgo: 4),
            new MemberBlock { InstitutionId = Inst, BlockerId = "them", BlockedId = "asker" });
        Assert.Equal(0, await rig.Activities.SendIntroductionRemindersAsync(Now));
    }

    [Fact]
    public async Task Nothing_is_sent_when_the_community_has_switched_introductions_off()
    {
        var off = new Institution { Id = Inst, Name = "Demo", Slug = "demo", Status = "Active", DisabledFeatures = [InstitutionFeatures.Introductions] };
        var rig = Rig(off, Person("asker"), Person("them"), Ask("a", daysAgo: 4));
        Assert.Equal(0, await rig.Activities.SendIntroductionRemindersAsync(Now));
    }
}

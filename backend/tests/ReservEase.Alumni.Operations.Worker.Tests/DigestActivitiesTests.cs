using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>Who gets the weekly digest and what is worth sending them. Emails are not delivered here (Temporal is off); a send is counted.</summary>
public class DigestActivitiesTests
{
    private const string Inst = "inst-1";

    private static MemberEntity Person(string id, int year, DateTime? created = null) => new()
    {
        Id = id, FirstName = id, LastName = "X", Email = $"{id}@x.org", Status = "Active", GraduationYear = year, InstitutionId = Inst, CreatedAt = created ?? DateTime.UtcNow.AddYears(-3),
    };

    private static ScheduledJobsRig Rig(params object[] seed)
    {
        var rig = new ScheduledJobsRig();
        rig.Seed(new Institution { Id = Inst, Name = "Demo", Slug = "demo", EmailNotificationsEnabled = true, Status = "Active" }).GetAwaiter().GetResult();
        rig.Seed(seed).GetAwaiter().GetResult();
        return rig;
    }

    [Fact]
    public async Task A_member_with_nothing_to_hear_is_sent_nothing()
    {
        var rig = Rig(Person("a", 2015));
        Assert.Equal(0, await rig.Activities.SendDueDigestsForInstitutionAsync(Inst));
    }

    [Fact]
    public async Task What_is_happening_in_a_members_own_groups_is_reason_enough_to_send_and_other_groups_are_not()
    {
        var rig = Rig(
            Person("inGroup", 2015), Person("elsewhere", 2016), Person("alone", 2017),
            new Community { Id = "g1", Name = "Accra Chapter", IsActive = true, InstitutionId = Inst },
            new Community { Id = "g2", Name = "Choir", IsActive = true, InstitutionId = Inst },
            new CommunityMembership { CommunityId = "g1", MemberId = "inGroup", Status = "Approved", InstitutionId = Inst },
            new CommunityMembership { CommunityId = "g1", MemberId = "elsewhere", Status = "Pending", InstitutionId = Inst },
            new CommunityMembership { CommunityId = "g2", MemberId = "elsewhere", Status = "Approved", InstitutionId = Inst },
            new AlumniEvent { Id = "e1", Title = "Chapter dinner", Venue = "Hall", CommunityId = "g1", StartDate = DateTime.UtcNow.AddDays(5), Status = "Upcoming", InstitutionId = Inst });

        // Only the member who is in the group with the event has anything to hear; the one in another group, and the one in none, do not.
        Assert.Equal(1, await rig.Activities.SendDueDigestsForInstitutionAsync(Inst));
    }

    [Fact]
    public async Task New_classmates_from_your_year_are_news_but_you_are_not_counted_as_your_own_classmate()
    {
        var rig = Rig(
            Person("veteran", 2015),
            Person("fresh", 2015, DateTime.UtcNow.AddDays(-2)),        // joined this week, same year as the veteran
            Person("otherYear", 2019));

        // The veteran hears about the new arrival; the new member is the only new one in the year, so has no classmate news; the 2019 member neither.
        Assert.Equal(1, await rig.Activities.SendDueDigestsForInstitutionAsync(Inst));
    }
}

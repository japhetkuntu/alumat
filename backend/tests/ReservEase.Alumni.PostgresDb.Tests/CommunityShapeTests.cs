using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class CommunityShapeTests
{
    [Theory]
    [InlineData("Off", "Community")]
    [InlineData("Optional", "Alumni")]
    [InlineData("Required", "Alumni")]
    public void Choosing_how_graduation_years_are_used_keeps_the_older_type_in_step(string mode, string expectedType)
    {
        var i = new Institution();

        i.UseGraduationYears(mode);

        Assert.Equal((mode, expectedType), (i.GraduationYearMode, i.OrganizationType));
    }

    [Fact]
    public void An_unrecognised_mode_falls_back_to_the_behaviour_every_existing_community_has()
    {
        var i = new Institution();
        i.UseGraduationYears("nonsense");

        Assert.Equal(("Required", "Alumni"), (i.GraduationYearMode, i.OrganizationType));
    }

    [Fact]
    public void Dues_are_owed_from_the_graduation_year_or_from_joining_when_there_is_none()
    {
        var joined = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(2015, MembershipPeriods.RequiredFromYear(2015, joined));   // an alumnus who joins late still owes from 2015, as before
        Assert.Equal(2026, MembershipPeriods.RequiredFromYear(0, joined));      // no year: never for years before they were there
    }

    [Fact]
    public void Every_starting_point_is_valid_and_start_from_scratch_assumes_nothing()
    {
        Assert.All(InstitutionTemplates.All, t => Assert.True(GraduationYearModes.IsValid(t.GraduationYearMode)));
        Assert.Equal(InstitutionTemplates.All.Count, InstitutionTemplates.All.Select(t => t.Id).Distinct().Count());

        var blank = InstitutionTemplates.Find("BLANK")!;
        Assert.Equal(("Off", null, null), (blank.GraduationYearMode, blank.CohortLabel, blank.CohortLabelPlural));
        Assert.Null(InstitutionTemplates.Find("no-such-thing"));
    }

    [Fact]
    public void Going_to_no_years_stops_asking_for_an_id_once_and_never_overrides_a_later_choice()
    {
        var i = new Institution();
        Assert.True(i.CollectMemberId);               // existing communities keep asking

        i.UseGraduationYears("Off");
        Assert.False(i.CollectMemberId);              // a fresh community with no years starts without an ID field

        i.CollectMemberId = true;                     // an admin switches it back on
        i.UseGraduationYears("Off");                  // saving the same setting again must not undo that
        Assert.True(i.CollectMemberId);
    }
}

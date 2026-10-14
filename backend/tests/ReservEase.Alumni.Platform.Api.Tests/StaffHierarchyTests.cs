using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Tests;

public class StaffHierarchyTests
{
    private static PlatformStaff P(string id, string role, string? reportsTo = null, bool disabled = false)
        => new() { Id = id, Name = id, Email = $"{id}@x.com", Role = role, ReportsToStaffId = reportsTo, IsDisabled = disabled };

    // boss (SuperAdmin) ── ama ── kofi ── yaw
    //                 └─ esi
    private static StaffHierarchy Team() => new([
        P("boss", "SuperAdmin"), P("ama", "Representative", "boss"), P("esi", "Representative", "boss"),
        P("kofi", "Representative", "ama"), P("yaw", "Representative", "kofi"), P("billing", "Billing"),
    ]);

    [Fact]
    public void A_person_sees_themselves_and_everyone_below_them_through_any_number_of_levels()
    {
        var t = Team();
        Assert.Equal(["ama", "kofi", "yaw"], t.SelfAndDescendants("ama").OrderBy(x => x));
        Assert.Equal(["yaw"], t.SelfAndDescendants("yaw"));
        Assert.Equal(5, t.SelfAndDescendants("boss").Count);   // boss, ama, esi, kofi, yaw
    }

    [Fact]
    public void A_colleague_in_another_branch_is_not_in_someones_view()
    {
        Assert.DoesNotContain("esi", Team().SelfAndDescendants("ama"));
        Assert.DoesNotContain("ama", Team().SelfAndDescendants("kofi"));   // and nobody sees upwards
    }

    [Fact]
    public void The_chain_above_someone_is_listed_nearest_first()
    {
        Assert.Equal(["kofi", "ama", "boss"], Team().ManagersOf("yaw").Select(m => m.Id));
        Assert.Empty(Team().ManagersOf("boss"));
    }

    [Theory]
    [InlineData("rep", "boss", null)]                                  // a rep may report to a SuperAdmin
    [InlineData("rep", "ama", null)]                                   // or to another rep
    [InlineData("rep", null, "must report")]                           // but must report to someone
    [InlineData("rep", "rep", "themselves")]
    [InlineData("rep", "billing", "SuperAdmin or to another")]         // not to Billing/Support staff
    [InlineData("rep", "ghost", "could not be found")]
    public void A_representative_must_report_to_a_superadmin_or_another_representative(string id, string? manager, string? problem)
    {
        var t = new StaffHierarchy([.. Team().SelfAndDescendants("boss").Select(x => P(x, x == "boss" ? "SuperAdmin" : "Representative")), P("billing", "Billing"), P("rep", "Representative")]);
        var result = t.ValidateManager(id, "Representative", manager);
        if (problem is null) Assert.Null(result); else Assert.Contains(problem, result);
    }

    [Fact]
    public void A_loop_is_refused_so_nobody_ends_up_reporting_to_themselves_through_others()
    {
        var t = Team();
        // ama is above yaw; making ama report to yaw would close the loop.
        Assert.Contains("loop", t.ValidateManager("ama", "Representative", "yaw"));
        Assert.Contains("loop", t.ValidateManager("ama", "Representative", "kofi"));
        Assert.Null(t.ValidateManager("kofi", "Representative", "esi"));    // moving a branch sideways is fine
    }

    [Fact]
    public void A_superadmin_reports_to_nobody_and_other_roles_are_not_part_of_the_structure()
    {
        var t = Team();
        Assert.Null(t.ValidateManager("boss", "SuperAdmin", null));
        Assert.NotNull(t.ValidateManager("boss", "SuperAdmin", "ama"));
        Assert.NotNull(t.ValidateManager("billing", "Billing", "boss"));
    }

    [Fact]
    public void A_disabled_manager_cannot_be_chosen()
    {
        var t = new StaffHierarchy([P("boss", "SuperAdmin"), P("old", "Representative", "boss", disabled: true), P("new", "Representative", "boss")]);
        Assert.Contains("disabled", t.ValidateManager("new", "Representative", "old"));
    }

    [Fact]
    public void The_team_cannot_be_nested_beyond_the_depth_limit()
    {
        var staff = new List<PlatformStaff> { P("boss", "SuperAdmin") };
        var parent = "boss";
        for (var i = 1; i <= StaffHierarchy.MaxDepth; i++) { staff.Add(P($"r{i}", "Representative", parent)); parent = $"r{i}"; }
        staff.Add(P("extra", "Representative", "boss"));

        var t = new StaffHierarchy(staff);

        Assert.Contains("levels deep", t.ValidateManager("extra", "Representative", parent));
    }

    [Fact]
    public void A_cycle_already_in_old_data_does_not_hang_anything()
    {
        var t = new StaffHierarchy([P("a", "Representative", "b"), P("b", "Representative", "a")]);

        Assert.Equal(["a", "b"], t.SelfAndDescendants("a").OrderBy(x => x));
        Assert.True(t.ManagersOf("a").Count <= StaffHierarchy.MaxDepth);
        Assert.True(t.DepthBelow("a") <= 2);
    }
}

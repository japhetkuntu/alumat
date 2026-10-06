using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Controllers;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class PledgesControllerTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public PledgesController Controller { get; private set; } = null!;
        public Rig() => Fresh();

        public PledgesController Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            var controller = new PledgesController(
                new AlumniPgRepository<Pledge>(db), new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<Contribution>(db),
                new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<MemberEntity>(db), NullLogger<PledgesController>.Instance);
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "m1"), new Claim(ClaimTypes.Email, "ama@x.com"),
                new Claim(ClaimTypes.GivenName, "Ama"), new Claim(ClaimTypes.Surname, "Mensah"), new Claim(ClaimTypes.Role, "Member")], "test");
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return Controller = controller;
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    private static Campaign Camp(bool pledges, Action<Campaign>? tweak = null)
    {
        var c = new Campaign { Id = "c1", Title = "Library", Deadline = DateTime.UtcNow.AddDays(30), Status = CampaignStatus.Active, AllowPledges = pledges };
        tweak?.Invoke(c);
        return c;
    }

    private static CreatePledgeRequest Request(string campaign = "c1") => new(campaign, 100m, DateTime.UtcNow.Date.AddDays(7));

    private static int Code(IActionResult result) => ((ObjectResult)result).StatusCode ?? 200;
    private static string? Message(IActionResult result) => (((ObjectResult)result).Value as dynamic)?.Message as string;

    [Fact]
    public async Task A_fundraiser_that_has_not_turned_pledges_on_refuses_them()
    {
        var rig = new Rig();
        await rig.Seed(Camp(pledges: false));

        var result = await rig.Controller.Create(Request());

        Assert.Equal(400, Code(result));
        Assert.Equal("This fundraiser isn't taking pledges.", Message(result));
        Assert.Equal(0, await rig.Db().Pledges.CountAsync());
    }

    [Fact]
    public async Task A_fundraiser_with_pledges_on_takes_one_and_saves_it_for_the_member()
    {
        var rig = new Rig();
        await rig.Seed(Camp(pledges: true));

        var result = await rig.Controller.Create(Request());

        Assert.Equal(200, Code(result));
        var pledge = await rig.Db().Pledges.SingleAsync();
        Assert.Equal(("m1", "c1", 100m, PledgeStatuses.Open), (pledge.MemberId, pledge.CampaignId, pledge.Amount, pledge.Status));
    }

    [Fact]
    public async Task Pledges_stay_off_for_a_fundraiser_even_if_the_flag_is_somehow_set_on_membership_dues()
    {
        var rig = new Rig();
        await rig.Seed(Camp(pledges: true, c => { c.IsMembershipCampaign = true; c.MembershipYear = DateTime.UtcNow.Year; }));
        Assert.Equal(404, Code(await rig.Controller.Create(Request())));
    }

    [Fact]
    public async Task The_setting_is_per_fundraiser_so_another_fundraiser_can_still_be_off()
    {
        var rig = new Rig();
        var off = Camp(pledges: false); off.Id = "c2";
        await rig.Seed(Camp(pledges: true), off);

        Assert.Equal(200, Code(await rig.Controller.Create(Request("c1"))));
        rig.Fresh();
        Assert.Equal(400, Code(await rig.Controller.Create(Request("c2"))));
    }

    [Fact]
    public async Task A_pledge_made_before_pledging_was_turned_off_can_still_be_seen_and_cancelled()
    {
        var rig = new Rig();
        await rig.Seed(Camp(pledges: false),
            new Pledge { Id = "p1", CampaignId = "c1", MemberId = "m1", Amount = 50, DueDate = DateTime.UtcNow.AddDays(3), MemberName = "Ama" });

        var mine = (ApiResponse<List<MemberPledgeDto>>)((ObjectResult)await rig.Controller.GetMine()).Value!;
        Assert.Single(mine.Data!);

        rig.Fresh();
        Assert.Equal(200, Code(await rig.Controller.Cancel("p1")));
        Assert.Equal(PledgeStatuses.Cancelled, (await rig.Db().Pledges.SingleAsync()).Status);
    }

    [Fact]
    public async Task The_off_check_comes_after_the_fundraiser_is_known_to_exist_and_be_open()
    {
        var rig = new Rig();
        await rig.Seed(Camp(pledges: false, c => c.Status = CampaignStatus.Closed));
        var closed = await rig.Controller.Create(Request());
        Assert.Contains("no longer accepting", Message(closed));

        Assert.Equal(404, Code(await rig.Controller.Create(Request("ghost"))));
    }
}

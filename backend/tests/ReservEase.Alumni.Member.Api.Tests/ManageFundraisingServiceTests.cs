using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>A leader's fundraisers: only for their own group, never touching account details, and never showing fees or net amounts.</summary>
public class ManageFundraisingServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private const string Run = LeadershipPrivileges.ManageFundraisers;
    private const string See = LeadershipPrivileges.ViewGiving;

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CapturingLogger<ManageFundraisingService> Log { get; } = new();
        public FakeStorage Storage { get; } = new();

        public ManageFundraisingService Service(string tenant = Tenant)
        {
            var db = TestDb.Create(DbName, tenant);
            var access = new LeadershipAccess(new AlumniPgRepository<LeadershipPost>(db), new AlumniPgRepository<LeadershipRole>(db));
            var temporal = new Mock<ITemporalClientProvider>();
            temporal.SetupGet(t => t.IsAvailable).Returns(false);
            return new ManageFundraisingService(new GroupGuard(access, new AlumniPgRepository<Batch>(db), new AlumniPgRepository<Community>(db)), Storage.Media(TestDb.Tenant(tenant, "umat")),
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<Contribution>(db),
                new AlumniPgRepository<InstitutionAuditLogEntry>(db), TestDb.Tenant(tenant, "umat"), temporal.Object, Log);
        }

        public AlumniDbContext Db(string tenant = Tenant) => TestDb.Create(DbName, tenant);
        public int Queued(string kind) => Log.Entries.Count(e => e.Message.Contains($"dropped notification {kind} "));

        private static MemberEntity Person(string id, string tenant = Tenant) => new() { Id = id, InstitutionId = tenant, FirstName = id, LastName = "Test", Email = $"{id}@x.test", Status = "Active" };

        /// <summary>
        /// ama runs fundraisers for 2015, bo only sees the giving for 2015, cat does both for chapter c1, esi does both everywhere, yaa holds a title only.
        /// Campaigns: "c15" for 2015 (made by an admin), "multi" for 2015 and 2016, "dues" a membership campaign for 2015, "chapter" for c1.
        /// </summary>
        public async Task Seed()
        {
            using var db = Db();
            db.AddRange(
                new Batch { Id = "b2015", InstitutionId = Tenant, Name = "Class of 2015", Year = 2015 }, new Batch { Id = "b2016", InstitutionId = Tenant, Name = "Class of 2016", Year = 2016 },
                new Community { Id = "c1", InstitutionId = Tenant, Name = "Accra Chapter" },
                Person("ama"), Person("bo"), Person("cat"), Person("esi"), Person("yaa"), Person("ben"), Person("gus"),
                new LeadershipRole { Id = "run", InstitutionId = Tenant, Name = "Fundraiser", Privileges = [Run] },
                new LeadershipRole { Id = "see", InstitutionId = Tenant, Name = "Treasurer", Privileges = [See] },
                new LeadershipRole { Id = "both", InstitutionId = Tenant, Name = "Finance lead", Privileges = [Run, See] },
                new LeadershipRole { Id = "title", InstitutionId = Tenant, Name = "Honorary", Privileges = [] },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "ama", RoleId = "run", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "bo", RoleId = "see", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "cat", RoleId = "both", ScopeType = "Community", ScopeId = "c1" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "esi", RoleId = "both", ScopeType = "Institution", ScopeId = "" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "yaa", RoleId = "title", ScopeType = "Batch", ScopeId = "b2015" },
                new Campaign { Id = "c15", InstitutionId = Tenant, Title = "2015 scholarship", TargetAmount = 5000, AmountPerMember = 50, Deadline = DateTime.UtcNow.AddDays(30), YearGroups = [2015], CreatedBy = "admin1", CollectedAmount = 50, PaidCount = 1 },
                new Campaign { Id = "multi", InstitutionId = Tenant, Title = "2015 and 2016 hall", TargetAmount = 9000, AmountPerMember = 20, Deadline = DateTime.UtcNow.AddDays(30), YearGroups = [2015, 2016], CreatedBy = "admin1" },
                new Campaign { Id = "dues", InstitutionId = Tenant, Title = "Dues 2026", TargetAmount = 100, AmountPerMember = 10, Deadline = DateTime.UtcNow.AddDays(30), YearGroups = [2015], IsMembershipCampaign = true, CreatedBy = "admin1" },
                new Campaign { Id = "chapter", InstitutionId = Tenant, Title = "Chapter hall", TargetAmount = 3000, AmountPerMember = 30, Deadline = DateTime.UtcNow.AddDays(30), CommunityId = "c1", CreatedBy = "admin1" },
                new Contribution { Id = "g1", InstitutionId = Tenant, CampaignId = "c15", MemberId = "ben", Amount = 50, Status = "Successful", PaymentMethod = "Paystack", TransactionRef = "REF123", ProofUrl = "proof.png",
                    PlatformFeeAmount = 5, NetAmountToInstitution = 45, PlatformRevenueAmount = 2, GrossChargeAmount = 55, GatewayFeeAmount = 3 },
                new Contribution { Id = "g2", InstitutionId = Tenant, CampaignId = "c15", MemberId = "yaa", Amount = 20, Status = "Pending", PaymentMethod = "Manual" },
                new Contribution { Id = "g3", InstitutionId = Tenant, CampaignId = "c15", MemberId = "gus", Amount = 10, Status = "Rejected", PaymentMethod = "Manual" },
                new Contribution { Id = "g4", InstitutionId = Tenant, CampaignId = "multi", MemberId = "ben", Amount = 20, Status = "Successful", PaymentMethod = "Paystack" });
            await db.SaveChangesAsync();

            using var other = Db(Other);
            other.AddRange(Person("xama", Other), new Batch { Id = "bx", InstitutionId = Other, Name = "Theirs", Year = 2015 },
                new LeadershipRole { Id = "xall", InstitutionId = Other, Name = "Patron", Privileges = [Run, See] },
                new LeadershipPost { InstitutionId = Other, MemberId = "xama", RoleId = "xall", ScopeType = "Institution", ScopeId = "" });
            await other.SaveChangesAsync();
        }
    }

    private static async Task<Rig> Seeded() { var r = new Rig(); await r.Seed(); return r; }

    private static SaveGroupCampaignRequest Req(string title = "Library fund", decimal target = 2000, decimal each = 40, DateTime? deadline = null)
        => new() { Title = title, Description = "Books for the library.", TargetAmount = target, AmountPerMember = each, Deadline = deadline ?? DateTime.UtcNow.AddDays(45) };

    // ── Starting one ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_starts_a_fundraiser_for_their_group_that_cannot_carry_account_details_or_manual_payments()
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", Req());

        Assert.Equal(201, result.Code);
        var c = await rig.Db().Set<Campaign>().SingleAsync(x => x.Title == "Library fund");
        Assert.Equal(("ama", CampaignStatus.Active, 2000m, 40m), (c.CreatedBy, c.Status, c.TargetAmount, c.AmountPerMember));
        Assert.Equal([2015], c.YearGroups);
        Assert.Null(c.CommunityId);
        Assert.False(c.AllowManualPayments);   // manual payments only exist if an administrator turns them on
        Assert.False(c.AllowPledges);
        Assert.False(c.IsMembershipCampaign);
        Assert.Null(c.BankAccount);
        Assert.Null(c.MobileMoneyAccount);
        Assert.Equal(1, rig.Queued("CampaignAlert"));
        var audit = await rig.Db().Set<InstitutionAuditLogEntry>().SingleAsync();
        Assert.Equal(("Fundraiser Started", "ama Test (leader)"), (audit.Action, audit.Actor));
    }

    [Fact]
    public async Task The_request_has_no_place_to_put_account_details_so_a_leader_cannot_redirect_money()
    {
        var names = typeof(SaveGroupCampaignRequest).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(names, n => new[] { "Bank", "Account", "Mobile", "Momo", "Branch", "Paystack", "Subaccount", "Manual", "Pledge", "Membership" }.Any(w => n.Contains(w, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(["AmountPerMember", "BannerImage", "Deadline", "Description", "ExistingImageUrls", "Images", "RemoveBanner", "TargetAmount", "Title", "YoutubeVideoUrls"], names.Order().ToArray());
    }

    [Fact]
    public async Task A_community_leaders_fundraiser_belongs_to_that_community()
    {
        var rig = await Seeded();

        Assert.Equal(201, (await rig.Service().CreateCampaignAsync("cat", "Community", "c1", Req())).Code);

        var c = await rig.Db().Set<Campaign>().SingleAsync(x => x.Title == "Library fund");
        Assert.Equal("c1", c.CommunityId);
        Assert.Null(c.YearGroups);
    }

    [Theory]
    [InlineData("bo", "Batch", "b2015")]       // sees the giving, may not start one
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("ben", "Batch", "b2015")]
    [InlineData("ama", "Batch", "b2016")]      // runs fundraisers, but for another year group
    [InlineData("ama", "Community", "c1")]
    [InlineData("cat", "Community", "x")]
    [InlineData("xama", "Batch", "b2015")]     // a patron of another institution
    public async Task A_fundraiser_cannot_be_started_without_the_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateCampaignAsync(who, type, id, Req());

        Assert.True(result.Code is 403 or 404);
        Assert.Equal(4, await rig.Db().Set<Campaign>().CountAsync());   // only the seeded ones
        Assert.Equal(0, rig.Queued("CampaignAlert"));
    }

    [Fact]
    public async Task A_bad_fundraiser_is_refused_and_nothing_is_saved()
    {
        var rig = await Seeded();
        var s = rig.Service();

        Assert.Equal(400, (await s.CreateCampaignAsync("ama", "Batch", "b2015", Req(title: " "))).Code);
        Assert.Equal(400, (await s.CreateCampaignAsync("ama", "Batch", "b2015", Req(target: 0))).Code);
        Assert.Equal(400, (await s.CreateCampaignAsync("ama", "Batch", "b2015", Req(target: -5))).Code);
        Assert.Equal(400, (await s.CreateCampaignAsync("ama", "Batch", "b2015", Req(each: 0))).Code);
        Assert.Equal(400, (await s.CreateCampaignAsync("ama", "Batch", "b2015", Req(target: 999_999_999))).Code);
        Assert.Equal(400, (await s.CreateCampaignAsync("ama", "Batch", "b2015", Req(deadline: DateTime.UtcNow.AddDays(-1)))).Code);
        Assert.Equal(4, await rig.Db().Set<Campaign>().CountAsync());
    }

    // ── What a leader sees ───────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_sees_only_fundraisers_aimed_at_exactly_their_group_and_never_membership_dues()
    {
        var rig = await Seeded();

        var ama = (await rig.Service().GetCampaignsAsync("ama", "Batch", "b2015")).Data!;
        var cat = (await rig.Service().GetCampaignsAsync("cat", "Community", "c1")).Data!;

        Assert.Equal(["2015 scholarship"], ama.Select(c => c.Title).ToArray());   // not "2015 and 2016 hall" (shared with another year group), not the dues
        Assert.Equal(["Chapter hall"], cat.Select(c => c.Title).ToArray());
        Assert.Equal(["2015 scholarship"], (await rig.Service().GetCampaignsAsync("esi", "Batch", "b2015")).Data!.Select(c => c.Title).ToArray());   // even a leader of everything sees a shared fundraiser through none of its groups
        Assert.Empty((await rig.Service().GetCampaignsAsync("esi", "Batch", "b2016")).Data!);
    }

    [Fact]
    public async Task What_a_leader_can_do_with_each_fundraiser_follows_their_privileges()
    {
        var rig = await Seeded();
        await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", Req("Mine"));

        var ama = (await rig.Service().GetCampaignsAsync("ama", "Batch", "b2015")).Data!;
        var bo = (await rig.Service().GetCampaignsAsync("bo", "Batch", "b2015")).Data!;

        Assert.Equal(["Mine"], ama.Where(c => c.CanEdit).Select(c => c.Title).ToArray());   // hers, not the administrator's
        Assert.All(ama, c => Assert.False(c.CanSeeGiving));                                  // she may run them, not see who gave
        Assert.All(bo, c => { Assert.False(c.CanEdit); Assert.True(c.CanSeeGiving); });
    }

    [Theory]
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("ben", "Batch", "b2015")]
    [InlineData("ama", "Batch", "b2016")]
    [InlineData("bo", "Community", "c1")]
    [InlineData("xama", "Batch", "b2015")]
    public async Task The_list_is_refused_without_either_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().GetCampaignsAsync(who, type, id)).Code);
    }

    // ── Changing and closing ─────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_edits_and_closes_only_their_own_active_fundraisers_in_their_group()
    {
        var rig = await Seeded();
        var mine = (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", Req("Mine"))).Data!;

        Assert.Equal(200, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", mine.Id, Req("Mine, renamed", target: 3000))).Code);
        Assert.Equal(404, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", "c15", Req("Hijack"))).Code);     // an administrator's
        Assert.Equal(404, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", "multi", Req("Hijack"))).Code);   // shared with another year group
        Assert.Equal(404, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", "dues", Req("Hijack"))).Code);    // membership dues
        Assert.Equal(404, (await rig.Service().CloseCampaignAsync("ama", "Batch", "b2015", "c15")).Code);
        Assert.Equal(200, (await rig.Service().CloseCampaignAsync("ama", "Batch", "b2015", mine.Id)).Code);

        Assert.Equal(CampaignStatus.Closed, (await rig.Db().Set<Campaign>().SingleAsync(c => c.Id == mine.Id)).Status);
        Assert.Equal("2015 scholarship", (await rig.Db().Set<Campaign>().SingleAsync(c => c.Id == "c15")).Title);
        Assert.Equal(404, (await rig.Service().CloseCampaignAsync("ama", "Batch", "b2015", mine.Id)).Code);                  // already closed
        Assert.Equal(404, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", mine.Id, Req("Reopen?"))).Code);
    }

    [Fact]
    public async Task A_fundraiser_cannot_be_changed_through_a_different_group()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateCampaignAsync("esi", "Batch", "b2015", Req("2015 drive"))).Data!;

        Assert.Equal(404, (await rig.Service().UpdateCampaignAsync("esi", "Batch", "b2016", made.Id, Req("Moved"))).Code);
        Assert.Equal(404, (await rig.Service().UpdateCampaignAsync("esi", "Community", "c1", made.Id, Req("Moved"))).Code);
        Assert.Equal(404, (await rig.Service().CloseCampaignAsync("esi", "Batch", "b2016", made.Id)).Code);
    }

    [Fact]
    public async Task Once_someone_has_given_the_amount_asked_of_each_member_is_fixed_but_the_rest_can_change()
    {
        var rig = await Seeded();
        var mine = (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", Req("Mine", each: 40))).Data!;
        using (var db = rig.Db()) { var c = await db.Set<Campaign>().SingleAsync(x => x.Id == mine.Id); c.PaidCount = 1; c.CollectedAmount = 40; await db.SaveChangesAsync(); }

        var amount = await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", mine.Id, Req("Mine", each: 60));
        Assert.Equal(400, amount.Code);
        Assert.Contains("already given", amount.Message);
        Assert.Equal(200, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", mine.Id, Req("Mine, bigger goal", target: 5000, each: 40))).Code);
        Assert.Equal(40m, (await rig.Db().Set<Campaign>().SingleAsync(c => c.Id == mine.Id)).AmountPerMember);
    }

    [Theory]
    [InlineData("bo")]     // may only look
    [InlineData("yaa")]
    public async Task Editing_and_closing_need_the_privilege_to_run_fundraisers(string who)
    {
        var rig = await Seeded();
        var mine = (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", Req("Mine"))).Data!;

        Assert.Equal(403, (await rig.Service().UpdateCampaignAsync(who, "Batch", "b2015", mine.Id, Req("Changed"))).Code);
        Assert.Equal(403, (await rig.Service().CloseCampaignAsync(who, "Batch", "b2015", mine.Id)).Code);
        Assert.Equal(CampaignStatus.Active, (await rig.Db().Set<Campaign>().SingleAsync(c => c.Id == mine.Id)).Status);
    }

    // ── Who has given ────────────────────────────────────────────────────────

    [Fact]
    public async Task The_giving_shows_who_gave_how_much_and_whether_it_has_landed_but_not_rejected_gifts()
    {
        var rig = await Seeded();

        var gifts = (await rig.Service().GetGiftsAsync("bo", "Batch", "b2015", "c15")).Data!;

        Assert.Equal([("ben Test", 50m, "Received", "Online"), ("yaa Test", 20m, "Waiting for confirmation", "Bank or mobile money")],
            gifts.Select(g => (g.GiverName, g.Amount, g.Status, g.Method)).OrderBy(g => g.Item2 < 50).ToArray());
    }

    [Theory]
    [InlineData("ama", "Batch", "b2015", "c15")]       // runs fundraisers, may not see who gave
    [InlineData("yaa", "Batch", "b2015", "c15")]
    [InlineData("bo", "Batch", "b2016", "c15")]        // another year group
    [InlineData("bo", "Community", "c1", "c15")]
    [InlineData("xama", "Batch", "b2015", "c15")]
    public async Task The_giving_is_refused_without_the_privilege_in_that_exact_group(string who, string type, string id, string campaign)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().GetGiftsAsync(who, type, id, campaign)).Code);
    }

    [Theory]
    [InlineData("multi")]      // shared with another year group: its givers belong to both
    [InlineData("dues")]       // membership dues are not a fundraiser
    [InlineData("chapter")]    // another group's
    [InlineData("nope")]
    public async Task The_giving_of_a_fundraiser_that_is_not_exactly_this_groups_is_not_found(string campaign)
    {
        var rig = await Seeded();

        Assert.Equal(404, (await rig.Service().GetGiftsAsync("bo", "Batch", "b2015", campaign)).Code);
        Assert.Equal(404, (await rig.Service().GetGiftsAsync("esi", "Batch", "b2015", campaign)).Code);
    }

    [Fact]
    public void Nothing_a_leader_is_sent_can_carry_fees_net_amounts_references_proof_or_account_details()
    {
        var forbidden = new[] { "Fee", "Net", "Revenue", "Gross", "Gateway", "Reference", "Transaction", "Proof", "Subaccount", "Bank", "Account", "Mobile", "Disburse", "Payout", "Email", "Phone" };

        foreach (var type in new[] { typeof(GroupCampaignDto), typeof(GroupGiftDto) })
            foreach (var property in type.GetProperties())
                Assert.DoesNotContain(forbidden, w => property.Name.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_patron_of_another_institution_gets_nothing_here()
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().GetCampaignsAsync("xama", "Batch", "b2015")).Code);
        Assert.Equal(403, (await rig.Service().GetGiftsAsync("xama", "Batch", "b2015", "c15")).Code);
        Assert.Equal(404, (await rig.Service(Other).GetGiftsAsync("xama", "Batch", "bx", "c15")).Code);
    }

    // ── Picture and video ────────────────────────────────────────────────────

    [Fact]
    public async Task A_fundraiser_can_carry_a_banner_and_one_video()
    {
        var rig = await Seeded();
        var req = Req(); req.BannerImage = Files.Jpeg(); req.YoutubeVideoUrls = ["https://youtu.be/abc", "https://youtu.be/second"];

        var made = (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", req)).Data!;

        var c = await rig.Db().Set<Campaign>().SingleAsync(x => x.Id == made.Id);
        Assert.StartsWith("https://cdn.test/", c.BannerImageUrl);
        Assert.Equal("https://youtu.be/abc", c.YoutubeVideoUrl);   // a fundraiser has a single video
        Assert.Equal((c.BannerImageUrl, c.YoutubeVideoUrl), (made.BannerImageUrl, made.YoutubeVideoUrl));
    }

    [Fact]
    public async Task The_banner_and_video_can_be_changed_or_removed_when_editing()
    {
        var rig = await Seeded();
        var first = Req(); first.BannerImage = Files.Png(); first.YoutubeVideoUrls = ["https://youtu.be/abc"];
        var made = (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", first)).Data!;

        var edit = Req("Renamed"); edit.RemoveBanner = true; edit.YoutubeVideoUrls = null;
        Assert.Equal(200, (await rig.Service().UpdateCampaignAsync("ama", "Batch", "b2015", made.Id, edit)).Code);

        var c = await rig.Db().Set<Campaign>().SingleAsync(x => x.Id == made.Id);
        Assert.Null(c.BannerImageUrl); Assert.Null(c.YoutubeVideoUrl);
    }

    [Fact]
    public async Task A_bad_banner_or_a_video_that_is_not_youtube_refuses_the_fundraiser()
    {
        var rig = await Seeded();
        var banner = Req(); banner.BannerImage = Files.Disguised();
        var video = Req(); video.YoutubeVideoUrls = ["https://vimeo.com/1"];

        Assert.Equal(400, (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", banner)).Code);
        Assert.Equal(400, (await rig.Service().CreateCampaignAsync("ama", "Batch", "b2015", video)).Code);
        Assert.Equal(4, await rig.Db().Set<Campaign>().CountAsync());   // only the seeded ones
        Assert.Empty(rig.Storage.Uploaded);
    }
}

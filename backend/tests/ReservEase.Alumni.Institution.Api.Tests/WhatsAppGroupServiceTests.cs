using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class WhatsAppGroupServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin", FirstName = "Ama", LastName = "Admin", Role = "SuperAdmin" };

    private static WhatsAppGroupLink Group(string id, string status, string? jid = "120363@g.us", string? community = null, bool publishing = true) => new()
    {
        Id = id, Name = $"Group {id}", GroupJid = jid, Status = status, CommunityId = community, PublishingEnabled = publishing,
    };

    private static (WhatsAppGroupService s, string db, Mock<IInstitutionAuditLogService> audit) Create(bool platformOn, WhatsAppGroupLink[] groups, params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.Add(new PostgresDb.Sdk.Entities.Institution { Id = Tenant, Slug = "umat", Name = "UMaT", PortalName = "UMaT Alumni", Status = "Active", WhatsAppGroups = [.. groups] });
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name, Tenant);
        var audit = new Mock<IInstitutionAuditLogService>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WaSenderConfig:Enabled"] = platformOn ? "true" : "false", ["WaSenderConfig:AssistantNumber"] = "233200000000", ["MemberPortalBaseDomain"] = "alumunion.test",
        }).Build();
        var s = new WhatsAppGroupService(
            new AlumniPgRepository<PostgresDb.Sdk.Entities.Institution>(ctx), new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<AlumniEvent>(ctx),
            new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<NewsPost>(ctx), new AlumniPgRepository<Campaign>(ctx),
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<NotificationPreference>(ctx), new AlumniPgRepository<Referral>(ctx),
            TestDb.Tenant(Tenant, "umat"), audit.Object, Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false), config,
            NullLogger<WhatsAppGroupService>.Instance);
        return (s, name, audit);
    }

    private static PostgresDb.Sdk.Entities.Institution Reload(string db)
    {
        using var ctx = TestDb.Create(db, Tenant);
        return ctx.Institutions.Single(i => i.Id == Tenant);
    }

    // ── Adoption and invite links ────────────────────────────────────────

    [Fact]
    public async Task Adoption_counts_connected_members_alerts_and_usage_by_month_and_never_other_institutions()
    {
        var thisMonth = MessagingUsageKeys.For(DateTime.UtcNow);
        var (s, _, _) = Create(true, [],
            new MemberEntity { Id = "m1", InstitutionId = Tenant, Status = "Active", Email = "a@x.com", FirstName = "A", LastName = "B", PhoneVerifiedAt = DateTime.UtcNow },
            new MemberEntity { Id = "m2", InstitutionId = Tenant, Status = "Active", Email = "b@x.com", FirstName = "A", LastName = "B" },
            new MemberEntity { Id = "m3", InstitutionId = "other", Status = "Active", Email = "c@x.com", FirstName = "A", LastName = "B", PhoneVerifiedAt = DateTime.UtcNow },
            new NotificationPreference { MemberId = "m1", InstitutionId = Tenant, WhatsAppAlerts = true },
            new Referral { InstitutionId = Tenant, ReferrerId = "m1", ReferredEmail = "n@x.com", Channel = "whatsapp", Status = "Registered" },
            new Referral { InstitutionId = Tenant, ReferrerId = "m1", ReferredEmail = "o@x.com", Channel = "whatsapp", Status = "Pending" },
            new Referral { InstitutionId = Tenant, ReferrerId = "m1", ReferredEmail = "p@x.com", Channel = null, Status = "Pending" });
        // Usage lives on the institution row.
        var d0 = await s.GetAdoptionAsync();
        Assert.Equal(200, d0.Code);

        var d = d0.Data!;
        Assert.Equal((2, 1, 1), (d.ActiveMembers, d.Connected, d.AlertsOn));
        Assert.Equal(6, d.Months.Count);
        Assert.Equal(thisMonth, d.Months[^1].Month);
        Assert.Equal(("whatsapp", 2, 1), (d.ReferralsLast90Days[0].Channel, d.ReferralsLast90Days[0].Invited, d.ReferralsLast90Days[0].Registered));
        Assert.Contains(d.ReferralsLast90Days, c => c.Channel == "other" && c.Invited == 1);
    }

    [Theory]
    [InlineData("https://chat.whatsapp.com/AbCdEfGhIjKlMn", true)]
    [InlineData("https://chat.whatsapp.com.evil.test/AbCdEfGhIjKlMn", false)]
    [InlineData("http://chat.whatsapp.com/AbCdEfGhIjKlMn", false)]
    [InlineData("https://evil.test/https://chat.whatsapp.com/AbCdEfGhIjKlMn", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task Only_a_whatsapp_group_invite_address_can_be_saved_as_the_join_link(string link, bool accepted)
    {
        var (s, db, _) = Create(true, [Group("g1", WhatsAppGroupStatuses.Active)]);

        var r = await s.UpdateAsync("g1", new UpdateWhatsAppGroupRequest(null, true, null, true, link), Admin);

        Assert.Equal(accepted, r.Code == 200);
        Assert.Equal(accepted ? link : null, Reload(db).WhatsAppGroups.Single().InviteLink);
    }

    [Fact]
    public async Task Leaving_the_invite_link_out_keeps_it_and_an_empty_one_removes_it()
    {
        var g = Group("g1", WhatsAppGroupStatuses.Active);
        g.InviteLink = "https://chat.whatsapp.com/AbCdEfGhIjKlMn";
        var (s, db, _) = Create(true, [g]);

        await s.UpdateAsync("g1", new UpdateWhatsAppGroupRequest("Renamed", true, null, true), Admin);
        Assert.Equal("https://chat.whatsapp.com/AbCdEfGhIjKlMn", Reload(db).WhatsAppGroups.Single().InviteLink);

        await s.UpdateAsync("g1", new UpdateWhatsAppGroupRequest(null, true, null, true, ""), Admin);
        Assert.Null(Reload(db).WhatsAppGroups.Single().InviteLink);
    }

    // ── Connecting ───────────────────────────────────────────────────────

    [Fact]
    public async Task Starting_a_link_makes_a_code_that_is_shown_once_and_is_audited()
    {
        var (s, db, audit) = Create(true, []);

        var r = await s.CreateAsync(new CreateWhatsAppGroupRequest("Class of 2015", null), Admin);

        Assert.Equal(201, r.Code);
        Assert.Equal(WhatsAppGroupStatuses.AwaitingGroup, r.Data!.Status);
        Assert.Equal(8, r.Data.Code!.Length);
        Assert.True(r.Data.CodeExpiresAt > DateTime.UtcNow.AddHours(23));
        Assert.Single(Reload(db).WhatsAppGroups);
        audit.Verify(a => a.LogAsync(Admin, "WhatsApp Group Link Started", "Class of 2015"), Times.Once);
    }

    [Theory]
    [InlineData("")] [InlineData("x")]
    public async Task A_group_needs_a_real_name(string name)
    {
        var (s, _, _) = Create(true, []);
        Assert.Equal(400, (await s.CreateAsync(new CreateWhatsAppGroupRequest(name, null), Admin)).Code);
    }

    [Fact]
    public async Task A_community_link_must_be_to_a_community_that_exists_and_the_number_of_groups_is_capped()
    {
        var (s, _, _) = Create(true, []);
        Assert.Equal(404, (await s.CreateAsync(new CreateWhatsAppGroupRequest("Chapter", "ghost"), Admin)).Code);

        var full = Enumerable.Range(0, 10).Select(i => Group($"g{i}", WhatsAppGroupStatuses.Active)).ToArray();
        var (s2, _, _) = Create(true, full);
        Assert.Equal(400, (await s2.CreateAsync(new CreateWhatsAppGroupRequest("One too many", null), Admin)).Code);
    }

    [Fact]
    public async Task Only_a_group_that_answered_the_code_can_be_confirmed()
    {
        var (s, db, audit) = Create(true, [Group("waiting", WhatsAppGroupStatuses.AwaitingGroup, jid: null), Group("seen", WhatsAppGroupStatuses.Detected)]);

        Assert.Equal(400, (await s.ConfirmAsync("waiting", Admin)).Code);
        var ok = await s.ConfirmAsync("seen", Admin);

        Assert.Equal((200, WhatsAppGroupStatuses.Active), (ok.Code, ok.Data!.Status));
        var link = Reload(db).WhatsAppGroups.Single(g => g.Id == "seen");
        Assert.Equal(("admin", true), (link.ConfirmedBy, link.LinkedAt.HasValue));
        audit.Verify(a => a.LogAsync(Admin, "WhatsApp Group Connected", "Group seen"), Times.Once);
        Assert.Equal(404, (await s.ConfirmAsync("ghost", Admin)).Code);
    }

    [Fact]
    public async Task Settings_are_validated_and_all_commands_ticked_means_unrestricted()
    {
        var (s, db, _) = Create(true, [Group("g", WhatsAppGroupStatuses.Active)]);

        Assert.Equal(400, (await s.UpdateAsync("g", new UpdateWhatsAppGroupRequest(null, true, ["dance"], true), Admin)).Code);

        var some = await s.UpdateAsync("g", new UpdateWhatsAppGroupRequest("Renamed", true, ["!events", "help"], false), Admin);
        Assert.Equal((200, "Renamed", false), (some.Code, some.Data!.Name, some.Data.PublishingEnabled));
        Assert.Equal(["events", "help"], some.Data.AllowedCommands);

        var all = await s.UpdateAsync("g", new UpdateWhatsAppGroupRequest(null, true, [.. WhatsAppGroupCommands.All], true), Admin);
        Assert.Empty(all.Data!.AllowedCommands);
        Assert.Empty(Reload(db).WhatsAppGroups.Single().AllowedCommands);
    }

    [Fact]
    public async Task Disconnecting_removes_only_that_group()
    {
        var (s, db, _) = Create(true, [Group("a", WhatsAppGroupStatuses.Active), Group("b", WhatsAppGroupStatuses.Active, jid: "9@g.us")]);
        Assert.Equal(200, (await s.DeleteAsync("a", Admin)).Code);
        Assert.Equal(["b"], Reload(db).WhatsAppGroups.Select(g => g.Id));
    }

    [Fact]
    public async Task The_overview_shows_a_code_only_while_it_is_still_usable()
    {
        var expired = Group("old", WhatsAppGroupStatuses.AwaitingGroup, jid: null); expired.Code = "OLDCODE1"; expired.CodeExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        var live = Group("new", WhatsAppGroupStatuses.AwaitingGroup, jid: null); live.Code = "NEWCODE2"; live.CodeExpiresAt = DateTime.UtcNow.AddHours(2);
        var (s, _, _) = Create(true, [expired, live, Group("done", WhatsAppGroupStatuses.Active)]);

        var o = (await s.GetOverviewAsync()).Data!;

        Assert.True(o.PlatformEnabled);
        Assert.Equal("https://wa.me/233200000000?text=start%20umat", o.AssistantLink);
        Assert.Null(o.Groups.Single(g => g.Id == "old").Code);
        Assert.Equal("NEWCODE2", o.Groups.Single(g => g.Id == "new").Code);
        Assert.Null(o.Groups.Single(g => g.Id == "done").Code);
    }

    // ── Publishing ───────────────────────────────────────────────────────

    private static AlumniEvent Event(string id = "e1", string? community = null, List<int>? years = null) => new()
    {
        Id = id, InstitutionId = Tenant, Title = "Homecoming", StartDate = new DateTime(2026, 11, 14, 18, 0, 0), Venue = "Great Hall", Status = "Upcoming", CommunityId = community, YearGroups = years,
    };

    [Fact]
    public async Task A_preview_is_the_exact_message_with_the_exact_link_and_sends_nothing()
    {
        var (s, _, _) = Create(true, [Group("g", WhatsAppGroupStatuses.Active)], Event());

        var r = await s.PreviewAsync(new WhatsAppPublishRequest("event", "e1", "g", null));

        Assert.Equal("https://umat.alumunion.test/events/e1", r.Data!.Link);
        Assert.Contains("*Homecoming*", r.Data.Text);
        Assert.Contains("Great Hall", r.Data.Text);
        Assert.EndsWith("— UMaT Alumni", r.Data.Text);
    }

    [Theory]
    [InlineData("event")] [InlineData("job")] [InlineData("news")] [InlineData("campaign")]
    public async Task Each_kind_of_content_has_its_own_link_and_a_missing_item_is_refused(string type)
    {
        var (s, _, _) = Create(true, [Group("g", WhatsAppGroupStatuses.Active)],
            Event(), new Job { Id = "j1", InstitutionId = Tenant, Title = "Engineer", Company = "Acme", Location = "Accra", Type = "Full-time", Status = "Active" },
            new NewsPost { Id = "n1", InstitutionId = Tenant, Title = "News", Content = "x", Status = "Published" },
            new Campaign { Id = "c1", InstitutionId = Tenant, Title = "Fund", Status = CampaignStatus.Active, Deadline = new DateTime(2026, 12, 31) });
        var id = type switch { "event" => "e1", "job" => "j1", "news" => "n1", _ => "c1" };
        var path = type switch { "event" => "/events/e1", "job" => "/jobs/j1", "news" => "/news/n1", _ => "/payment-campaign/c1" };

        var ok = await s.PreviewAsync(new WhatsAppPublishRequest(type, id, "g", null));
        Assert.Equal($"https://umat.alumunion.test{path}", ok.Data!.Link);
        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest(type, "ghost", "g", null))).Code);
    }

    [Fact]
    public async Task Content_that_is_not_for_everyone_is_not_offered_to_a_group()
    {
        var (s, _, _) = Create(true, [Group("open", WhatsAppGroupStatuses.Active), Group("chapter", WhatsAppGroupStatuses.Active, jid: "9@g.us", community: "c-1")],
            Event("years", years: [2015]), Event("other", community: "c-2"), Event("mine", community: "c-1"));

        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "years", "open", null))).Code);
        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "other", "open", null))).Code);
        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "other", "chapter", null))).Code);
        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "mine", "open", null))).Code);   // a community's own item is not for the whole institution's group
        Assert.Equal(200, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "mine", "chapter", null))).Code);
    }

    [Fact]
    public async Task A_group_that_is_unconfirmed_or_publish_off_cannot_be_posted_to()
    {
        var (s, _, _) = Create(true, [Group("seen", WhatsAppGroupStatuses.Detected), Group("off", WhatsAppGroupStatuses.Active, publishing: false)], Event());

        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "e1", "seen", null))).Code);
        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "e1", "off", null))).Code);
        Assert.Equal(400, (await s.PreviewAsync(new WhatsAppPublishRequest("event", "e1", "ghost", null))).Code);
    }

    [Fact]
    public async Task Publishing_is_refused_while_the_platform_channel_is_off_or_the_text_is_too_long()
    {
        var (off, _, _) = Create(false, [Group("g", WhatsAppGroupStatuses.Active)], Event());
        Assert.Equal(400, (await off.PublishAsync(new WhatsAppPublishRequest("event", "e1", "g", null), Admin)).Code);

        var (on, _, _) = Create(true, [Group("g", WhatsAppGroupStatuses.Active)], Event());
        Assert.Equal(400, (await on.PublishAsync(new WhatsAppPublishRequest("event", "e1", "g", new string('x', 1001)), Admin)).Code);
    }

    [Fact]
    public async Task When_the_message_cannot_be_queued_the_administrator_is_told_and_nothing_is_logged_as_posted()
    {
        var (s, _, audit) = Create(true, [Group("g", WhatsAppGroupStatuses.Active)], Event());   // Temporal unavailable in this rig

        var r = await s.PublishAsync(new WhatsAppPublishRequest("event", "e1", "g", null), Admin);

        Assert.Equal(500, r.Code);
        audit.Verify(a => a.LogAsync(It.IsAny<AuthData>(), "Posted To WhatsApp Group", It.IsAny<string>()), Times.Never);
    }
}

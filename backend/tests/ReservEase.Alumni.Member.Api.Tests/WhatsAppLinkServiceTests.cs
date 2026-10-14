using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class WhatsAppLinkServiceTests
{
    private const string Sender = "233244123456";

    private static MemberEntity Person(string id, string inst, string email = "ama@x.com", string? phone = "0244123456", string status = "Active",
        bool emailVerified = true, DateTime? verified = null)
        => new() { Id = id, InstitutionId = inst, FirstName = "Ama", LastName = "M", Email = email, Phone = phone, Status = status, IsEmailVerified = emailVerified, PhoneVerifiedAt = verified };

    private sealed class Rig
    {
        public required WhatsAppLinkService Service { get; init; }
        public required string Db { get; init; }
        public required InMemoryRedisService<MemberRedisConfig> Redis { get; init; }
        public required CurrentTenantService Tenant { get; init; }
    }

    private static Rig Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }

        var tenant = TestDb.Tenant(null);
        var ctx = TestDb.Create(name, tenant: tenant);
        var redis = new InMemoryRedisService<MemberRedisConfig>();
        var service = new WhatsAppLinkService(
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<NotificationPreference>(ctx),
            new AlumniPgRepository<InstitutionAuditLogEntry>(ctx), tenant, redis,
            Options.Create(new WaSenderConfig { Enabled = true, AssistantNumber = "233200000000" }), NullLogger<WhatsAppLinkService>.Instance);
        return new Rig { Service = service, Db = name, Redis = redis, Tenant = tenant };
    }

    private static Institution Inst(string id, string name = "UMaT Alumni") => new() { Id = id, Slug = id, Name = name, PortalName = name, Status = "Active" };

    // The member sets are tenant-filtered; tests read across institutions.
    private static MemberEntity Load(string db, string id)
    {
        using var ctx = TestDb.Create(db);
        return ctx.Set<MemberEntity>().IgnoreQueryFilters().Single(m => m.Id == id);
    }

    private static async Task<string> NewCode(Rig rig, string memberId, string institutionId)
    {
        // The challenge records the institution the portal session was in, which is the tenant at that moment.
        rig.Tenant.SetInstitutionId(institutionId);
        return (await rig.Service.CreateChallengeAsync(memberId)).Code;
    }

    // ── Challenge ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_challenge_is_a_short_unambiguous_code_with_a_deep_link_to_the_assistant()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1"));

        var c = await rig.Service.CreateChallengeAsync("m1");

        Assert.Equal(8, c.Code.Length);
        Assert.DoesNotContain(c.Code, ch => "01OIL".Contains(ch));
        Assert.Equal($"https://wa.me/233200000000?text=LINK%20{c.Code}", c.DeepLink);
        Assert.Equal(600, c.ExpiresInSeconds);
    }

    [Fact]
    public async Task Asking_again_retires_the_earlier_code()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1"));
        var first = (await rig.Service.CreateChallengeAsync("m1")).Code;
        var second = (await rig.Service.CreateChallengeAsync("m1")).Code;

        Assert.NotEqual(first, second);
        Assert.Equal(LinkStatus.InvalidCode, (await rig.Service.CompleteAsync(first, Sender)).Status);
    }

    // ── Completing ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_different_number_from_the_account_waits_for_the_member_to_approve_it_in_the_portal()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1", phone: "0200000001"));
        var code = await NewCode(rig, "m1", "i1");

        var outcome = await rig.Service.CompleteAsync(code, Sender);

        Assert.Equal(LinkStatus.NeedsApproval, outcome.Status);
        var m = Load(rig.Db, "m1");
        Assert.Null(m.PhoneVerifiedAt);
        Assert.Equal("0200000001", m.Phone);
        Assert.NotNull((await rig.Service.GetStatusAsync("m1")).PendingNumber);

        // Only the signed-in member can approve; then it connects and the old number is replaced by the proven one.
        var approved = await rig.Service.ApproveAsync("m1");
        Assert.Equal(LinkStatus.Linked, approved.Status);
        m = Load(rig.Db, "m1");
        Assert.Equal(("+233244123456", "233244123456"), (m.Phone, m.PhoneNormalized));
        Assert.NotNull(m.PhoneVerifiedAt);
    }

    [Fact]
    public async Task Refusing_a_pending_number_connects_nothing()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1", phone: "0200000001"));
        var code = await NewCode(rig, "m1", "i1");
        await rig.Service.CompleteAsync(code, Sender);

        Assert.True(await rig.Service.RejectPendingAsync("m1"));

        Assert.Equal(LinkStatus.InvalidCode, (await rig.Service.ApproveAsync("m1")).Status);
        Assert.Null(Load(rig.Db, "m1").PhoneVerifiedAt);
    }

    [Fact]
    public async Task The_right_code_from_the_number_on_the_account_verifies_it_and_is_audited()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1", phone: "0244123456"));
        var code = await NewCode(rig, "m1", "i1");

        var outcome = await rig.Service.CompleteAsync(code, Sender);

        Assert.Equal((LinkStatus.Linked, "UMaT Alumni", "Ama"), (outcome.Status, outcome.CommunityName, outcome.FirstName));
        var m = Load(rig.Db, "m1");
        Assert.Equal(("+233244123456", "233244123456"), (m.Phone, m.PhoneNormalized));
        Assert.NotNull(m.PhoneVerifiedAt);
        using var ctx = TestDb.Create(rig.Db);
        var audit = ctx.Set<InstitutionAuditLogEntry>().IgnoreQueryFilters().Single();
        Assert.Equal(("WhatsApp connected", "m1"), (audit.Action, audit.ActorId));
    }

    [Fact]
    public async Task A_code_works_once_and_a_replay_does_nothing()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1"));
        var code = await NewCode(rig, "m1", "i1");
        Assert.Equal(LinkStatus.Linked, (await rig.Service.CompleteAsync(code, Sender)).Status);

        var replay = await rig.Service.CompleteAsync(code, "233500000000");

        Assert.Equal(LinkStatus.InvalidCode, replay.Status);
        Assert.Equal("233244123456", Load(rig.Db, "m1").PhoneNormalized);
    }

    [Fact]
    public async Task Lowercase_and_unknown_codes_behave_sensibly()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1"));
        var code = await NewCode(rig, "m1", "i1");

        Assert.Equal(LinkStatus.InvalidCode, (await rig.Service.CompleteAsync("ZZZZZZZZ", Sender)).Status);
        Assert.Equal(LinkStatus.Linked, (await rig.Service.CompleteAsync(code.ToLowerInvariant(), Sender)).Status);
    }

    [Fact]
    public async Task A_number_already_verified_on_another_member_is_refused_and_not_taken()
    {
        var rig = Create(Inst("i1"), Person("owner", "i1", email: "owner@x.com", verified: DateTime.UtcNow, phone: "+233244123456"), Person("squatter", "i1", email: "sq@x.com", phone: "0200000009"));
        var code = await NewCode(rig, "squatter", "i1");

        var outcome = await rig.Service.CompleteAsync(code, Sender);

        Assert.Equal(LinkStatus.Conflict, outcome.Status);
        Assert.NotNull(Load(rig.Db, "owner").PhoneVerifiedAt);
        Assert.Null(Load(rig.Db, "squatter").PhoneVerifiedAt);
        // The code is spent even though the link failed.
        Assert.Equal(LinkStatus.InvalidCode, (await rig.Service.CompleteAsync(code, Sender)).Status);
    }

    [Fact]
    public async Task An_account_that_is_no_longer_active_cannot_be_connected()
    {
        var rig = Create(Inst("i1"), Person("m1", "i1"));
        var code = await NewCode(rig, "m1", "i1");
        using (var db = TestDb.Create(rig.Db, "i1"))
        {
            var m = db.Set<MemberEntity>().Single(); m.Status = "Banned"; db.SaveChanges();
        }

        Assert.Equal(LinkStatus.NotAllowed, (await rig.Service.CompleteAsync(code, Sender)).Status);
    }

    // ── One person, several communities ──────────────────────────────────

    [Fact]
    public async Task The_same_verified_email_in_another_community_is_connected_too_but_only_where_safe()
    {
        var rig = Create(Inst("i1"), Inst("i2", "PENSA"), Inst("i3", "Other"), Inst("i4", "Different number"), Inst("i5", "Unverified email"),
            Person("a", "i1"),
            Person("b", "i2", phone: null),                        // no number yet: gets this one
            Person("c", "i3", phone: "0244123456"),                // already this number: verified
            Person("d", "i4", phone: "0501111111"),                // a different number: left alone
            Person("e", "i5", phone: null, emailVerified: false)); // email never proved there: left alone
        var code = await NewCode(rig, "a", "i1");

        var outcome = await rig.Service.CompleteAsync(code, Sender);

        Assert.Equal(2, outcome.AlsoLinked);
        Assert.NotNull(Load(rig.Db, "b").PhoneVerifiedAt);
        Assert.Equal("+233244123456", Load(rig.Db, "b").Phone);
        Assert.NotNull(Load(rig.Db, "c").PhoneVerifiedAt);
        Assert.Null(Load(rig.Db, "d").PhoneVerifiedAt);
        Assert.Equal("0501111111", Load(rig.Db, "d").Phone);
        Assert.Null(Load(rig.Db, "e").PhoneVerifiedAt);
    }

    // ── Disconnecting ────────────────────────────────────────────────────

    [Fact]
    public async Task Disconnecting_from_whatsapp_revokes_every_account_on_the_number_and_the_notification_consent()
    {
        var rig = Create(Inst("i1"), Inst("i2", "PENSA"),
            Person("a", "i1", verified: DateTime.UtcNow, phone: "+233244123456"), Person("b", "i2", verified: DateTime.UtcNow, phone: "+233244123456"),
            Person("other", "i1", email: "z@x.com", phone: "+233500000000", verified: DateTime.UtcNow),
            new NotificationPreference { MemberId = "a", InstitutionId = "i1", WhatsAppAlerts = true });

        var removed = await rig.Service.UnlinkAsync("0244123456");

        Assert.Equal(2, removed);
        Assert.Null(Load(rig.Db, "a").PhoneVerifiedAt);
        Assert.Null(Load(rig.Db, "b").PhoneVerifiedAt);
        Assert.NotNull(Load(rig.Db, "other").PhoneVerifiedAt);   // a different number is untouched
        using var ctx = TestDb.Create(rig.Db);
        Assert.False(ctx.Set<NotificationPreference>().IgnoreQueryFilters().Single().WhatsAppAlerts);
    }

    [Fact]
    public async Task Stop_turns_alerts_off_on_every_connected_account_but_keeps_the_connection_and_start_creates_nothing_for_a_guest()
    {
        var rig = Create(Inst("i1"), Inst("i2", "PENSA"),
            Person("a", "i1", verified: DateTime.UtcNow, phone: "+233244123456"), Person("b", "i2", verified: DateTime.UtcNow, phone: "+233244123456"),
            Person("guest", "i1", email: "g@x.com", phone: "0244123456"),
            new NotificationPreference { MemberId = "a", InstitutionId = "i1", WhatsAppAlerts = true });

        var changed = await rig.Service.SetAlertsAsync("0244123456", false);

        Assert.Equal(2, changed);
        Assert.NotNull(Load(rig.Db, "a").PhoneVerifiedAt);          // still connected
        using (var ctx = TestDb.Create(rig.Db))
            Assert.False(ctx.Set<NotificationPreference>().IgnoreQueryFilters().Single(p => p.MemberId == "a").WhatsAppAlerts);

        // Turning them on for an account that never had preferences creates them (opt-in is explicit and recorded).
        var on = await rig.Service.SetAlertsAsync("0244123456", true);
        Assert.Equal(2, on);
        using var ctx2 = TestDb.Create(rig.Db);
        Assert.True(ctx2.Set<NotificationPreference>().IgnoreQueryFilters().Single(p => p.MemberId == "a").WhatsAppAlerts);
        Assert.True(ctx2.Set<NotificationPreference>().IgnoreQueryFilters().Single(p => p.MemberId == "b").WhatsAppAlerts);
        Assert.DoesNotContain(ctx2.Set<NotificationPreference>().IgnoreQueryFilters(), p => p.MemberId == "guest");
    }

    [Fact]
    public async Task Disconnecting_in_the_portal_only_touches_the_signed_in_member()
    {
        var rig = Create(Inst("i1"), Person("a", "i1", verified: DateTime.UtcNow), Person("b", "i1", email: "b@x.com", phone: "+233500000000", verified: DateTime.UtcNow));
        rig.Tenant.SetInstitutionId("i1");

        Assert.True(await rig.Service.UnlinkMemberAsync("a"));
        Assert.False(await rig.Service.UnlinkMemberAsync("a"));
        Assert.Null(Load(rig.Db, "a").PhoneVerifiedAt);
        Assert.NotNull(Load(rig.Db, "b").PhoneVerifiedAt);
    }

    [Theory]
    [InlineData("platform-deactivated-for-this-institution")]
    [InlineData("institution-turned-it-off")]
    public async Task Status_says_unavailable_when_this_institution_is_switched_off_by_the_platform_or_by_itself(string how)
    {
        var inst = Inst("i1");
        if (how.StartsWith("platform")) inst.WhatsAppBotEnabled = false; else inst.DisabledFeatures = [InstitutionFeatures.WhatsApp];
        var rig = Create(inst, Inst("i2"), Person("a", "i1"), Person("b", "i2"));

        rig.Tenant.SetInstitutionId("i1");
        Assert.False((await rig.Service.GetStatusAsync("a")).Available);
        rig.Tenant.SetInstitutionId("i2");
        Assert.True((await rig.Service.GetStatusAsync("b")).Available);   // another institution is unaffected
    }

    [Fact]
    public async Task Status_shows_a_masked_number_and_never_the_whole_one()
    {
        var rig = Create(Inst("i1"), Person("a", "i1", verified: DateTime.UtcNow, phone: "+233244123456"));
        rig.Tenant.SetInstitutionId("i1");

        var status = await rig.Service.GetStatusAsync("a");

        Assert.True(status.Connected);
        Assert.Equal("+233 24 •••• 456", status.MaskedNumber);
        Assert.True(status.Available);
    }
}

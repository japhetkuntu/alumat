using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Operations.Worker.Workflows.Notifications;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.WebPush.Sdk.Services;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using Temporalio.Exceptions;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using PushSubscriptionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.PushSubscription;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public class NotificationDispatchActivitiesTests
{
    private const string Inst = "i1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<ISmsService> Sms { get; } = new();
        public Mock<IWhatsAppService> WhatsApp { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public Mock<IWebPushService> Push { get; } = new();
        public NotificationDispatchActivities Activities { get; private set; } = null!;
        public Dictionary<string, string?> Config { get; } = new() { ["MemberPortalBaseDomain"] = "members.test", ["AdminPortalBaseDomain"] = "admin.test" };

        public Rig() => Fresh();

        public NotificationDispatchActivities Fresh()
        {
            var tenant = TestDb.Tenant(null);   // shared with the context, as the scoped service is in production, so the activity's SetInstitutionId stamps rows
            var db = TestDb.Create(DbName, tenant: tenant);
            return Activities = new NotificationDispatchActivities(
                new AlumniPgRepository<Notification>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<NotificationPreference>(db),
                new AlumniPgRepository<AdminNotificationPreference>(db), new AlumniPgRepository<StaffEntity>(db), new AlumniPgRepository<Institution>(db),
                new AlumniPgRepository<Job>(db), new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<AlumniEvent>(db),
                new AlumniPgRepository<Spotlight>(db), new AlumniPgRepository<ClassNote>(db), new AlumniPgRepository<PushSubscriptionEntity>(db),
                tenant, Sms.Object, WhatsApp.Object, Email.Object, Push.Object,
                new ConfigurationBuilder().AddInMemoryCollection(Config).Build(), NullLogger<NotificationDispatchActivities>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) db.Add(e);
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    private static MemberEntity Member(string id, int year = 2015, string status = "Active", string? phone = "0241") =>
        new() { Id = id, InstitutionId = Inst, Status = status, GraduationYear = year, Phone = phone, Email = $"{id}@x.com", FirstName = id };

    private static StaffEntity Staff(string id, bool disabled = false) => new() { Id = id, InstitutionId = Inst, IsDisabled = disabled };

    // ── Institution info ────────────────────────────────────────────────

    [Fact]
    public async Task Institution_info_builds_portal_urls_from_slug_and_configured_domains()
    {
        var rig = new Rig();
        await rig.Seed(new Institution { Id = Inst, Name = "UMaT", PortalName = "UMaT Alumni", Slug = "umat", PrimaryColorHex = "#112233", SmsNotificationsEnabled = false });

        var info = (await rig.Activities.LoadInstitutionAsync(Inst))!;

        Assert.Equal(("UMaT", "UMaT Alumni", "#112233"), (info.Name, info.PortalName, info.PrimaryColorHex));
        Assert.Equal(("https://umat.members.test", "https://umat.admin.test"), (info.MemberPortalUrl, info.AdminPortalUrl));
        Assert.False(info.SmsNotificationsEnabled);
        Assert.True(info.EmailNotificationsEnabled);
    }

    [Fact]
    public async Task Portal_name_falls_back_to_the_institution_name_and_urls_are_empty_without_configured_domains()
    {
        var rig = new Rig();
        rig.Config.Clear();
        await rig.Seed(new Institution { Id = Inst, Name = "UMaT", PortalName = "  ", Slug = "umat" });

        var info = (await rig.Activities.LoadInstitutionAsync(Inst))!;

        Assert.Equal("UMaT", info.PortalName);
        Assert.Equal((string.Empty, string.Empty), (info.MemberPortalUrl, info.AdminPortalUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing")]
    public async Task Institution_info_is_null_for_a_blank_or_unknown_id(string id)
        => Assert.Null(await new Rig().Activities.LoadInstitutionAsync(id));

    // ── Content loaders ─────────────────────────────────────────────────

    [Fact]
    public async Task Content_loaders_return_the_fields_the_messages_need_and_null_when_missing()
    {
        var rig = new Rig();
        await rig.Seed(
            new Job { Id = "j", Title = "Engineer", Company = "Acme", Location = "Accra", YearGroups = [2018] },
            new Campaign { Id = "c", Title = "Library", YearGroups = [2019] },
            new AlumniEvent { Id = "e", Title = "Gala", Venue = "Hall", StartDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), YearGroups = [2020] },
            new Spotlight { Id = "s", Title = "Hero", Member = new MemberSnapshot { FirstName = "Ama", LastName = "M" } },
            new ClassNote { Id = "n", AuthorId = "a1", YearGroup = 2015 });

        var job = (await rig.Activities.LoadJobContentAsync("j"))!;
        Assert.Equal(("Engineer", "Acme", "Accra"), (job.Title, job.Company, job.Location));
        Assert.Equal(new[] { 2018 }, job.YearGroups);
        Assert.Equal("Library", (await rig.Activities.LoadCampaignContentAsync("c"))!.Title);
        var ev = (await rig.Activities.LoadEventContentAsync("e"))!;
        Assert.Equal(("Gala", "Hall"), (ev.Title, ev.Venue));
        var spot = (await rig.Activities.LoadSpotlightContentAsync("s"))!;
        Assert.Equal(("Hero", "Ama", "M"), (spot.Title, spot.MemberFirstName, spot.MemberLastName));
        var note = (await rig.Activities.LoadClassNoteContentAsync("n"))!;
        Assert.Equal(("a1", 2015), (note.AuthorId, note.YearGroup));

        Assert.Null(await rig.Activities.LoadJobContentAsync("x"));
        Assert.Null(await rig.Activities.LoadCampaignContentAsync("x"));
        Assert.Null(await rig.Activities.LoadEventContentAsync("x"));
        Assert.Null(await rig.Activities.LoadSpotlightContentAsync("x"));
        Assert.Null(await rig.Activities.LoadClassNoteContentAsync("x"));
    }

    // ── Member fan-out ──────────────────────────────────────────────────

    [Fact]
    public async Task Job_alerts_go_to_active_members_without_a_preference_row_and_skip_explicit_opt_outs()
    {
        var rig = new Rig();
        await rig.Seed(Member("a"), Member("b"), Member("c"),
            new NotificationPreference { InstitutionId = Inst, MemberId = "b", JobAlerts = false },
            new NotificationPreference { InstitutionId = Inst, MemberId = "c", JobAlerts = true });

        var recipients = await rig.Activities.ResolveJobAlertRecipientsAsync(Inst, null);

        Assert.Equal(new[] { "a", "c" }, recipients.Select(r => r.MemberId).OrderBy(x => x));
    }

    [Fact]
    public async Task Only_active_members_of_the_institution_are_notified()
    {
        var rig = new Rig();
        var elsewhere = Member("x"); elsewhere.InstitutionId = "other";
        await rig.Seed(Member("a"), Member("pending", status: "Pending"), Member("susp", status: "Suspended"), elsewhere);

        var recipients = await rig.Activities.ResolveSpotlightAlertRecipientsAsync(Inst);

        Assert.Equal(new[] { "a" }, recipients.Select(r => r.MemberId));
    }

    [Fact]
    public async Task Year_group_targeting_limits_job_campaign_and_event_alerts_and_empty_means_everyone()
    {
        var rig = new Rig();
        await rig.Seed(Member("y15", 2015), Member("y18", 2018), Member("y20", 2020));

        Assert.Equal(new[] { "y18" }, (await rig.Activities.ResolveJobAlertRecipientsAsync(Inst, [2018])).Select(r => r.MemberId));
        Assert.Equal(new[] { "y15", "y20" }, (await rig.Activities.ResolveCampaignAlertRecipientsAsync(Inst, [2015, 2020])).Select(r => r.MemberId).OrderBy(x => x));
        Assert.Equal(3, (await rig.Activities.ResolveEventReminderRecipientsAsync(Inst, [])).Count);
        Assert.Equal(3, (await rig.Activities.ResolveEventReminderRecipientsAsync(Inst, null)).Count);
    }

    [Fact]
    public async Task Each_alert_type_honours_its_own_opt_out_flag_only()
    {
        var rig = new Rig();
        await rig.Seed(Member("a"),
            new NotificationPreference { InstitutionId = Inst, MemberId = "a", JobAlerts = true, CampaignAlerts = false, EventReminders = true, SpotlightAlerts = false });

        Assert.Single(await rig.Activities.ResolveJobAlertRecipientsAsync(Inst, null));
        Assert.Empty(await rig.Activities.ResolveCampaignAlertRecipientsAsync(Inst, null));
        Assert.Single(await rig.Activities.ResolveEventReminderRecipientsAsync(Inst, null));
        Assert.Empty(await rig.Activities.ResolveSpotlightAlertRecipientsAsync(Inst));
    }

    [Fact]
    public async Task Recipients_carry_phone_and_sms_whatsapp_opt_ins_and_default_to_off_without_a_preference_row()
    {
        var rig = new Rig();
        await rig.Seed(Member("a", phone: "0241"), Member("b", phone: "0551"),
            new NotificationPreference { InstitutionId = Inst, MemberId = "a", SmsAlerts = true, WhatsAppAlerts = true });

        var recipients = (await rig.Activities.ResolveSpotlightAlertRecipientsAsync(Inst)).ToDictionary(r => r.MemberId);

        Assert.Equal(("0241", true, true), (recipients["a"].Phone, recipients["a"].SmsAlerts, recipients["a"].WhatsAppAlerts));
        Assert.Equal(("0551", false, false), (recipients["b"].Phone, recipients["b"].SmsAlerts, recipients["b"].WhatsAppAlerts));
    }

    [Fact]
    public async Task Class_note_alerts_go_to_the_same_year_excluding_the_author_and_anyone_opted_out()
    {
        var rig = new Rig();
        await rig.Seed(Member("author", 2015), Member("mate1", 2015), Member("mate2", 2015), Member("other-year", 2016),
            new NotificationPreference { InstitutionId = Inst, MemberId = "mate2", ClassNoteAlerts = false });

        var recipients = await rig.Activities.ResolveClassNoteAlertRecipientsAsync(Inst, 2015, "author");

        Assert.Equal(new[] { "mate1" }, recipients.Select(r => r.MemberId));
    }

    // ── Admin fan-out ───────────────────────────────────────────────────

    [Fact]
    public async Task Payment_alerts_go_to_enabled_staff_who_have_not_opted_out()
    {
        var rig = new Rig();
        await rig.Seed(Staff("s1"), Staff("s2"), Staff("s3", disabled: true), Staff("s4"),
            new AdminNotificationPreference { InstitutionId = Inst, StaffId = "s2", PaymentReceivedAlerts = false },
            new AdminNotificationPreference { InstitutionId = Inst, StaffId = "s4", PaymentReceivedAlerts = true });

        var ids = await rig.Activities.ResolvePaymentReceivedAdminRecipientsAsync(Inst);

        Assert.Equal(new[] { "s1", "s4" }, ids.OrderBy(x => x));
    }

    [Fact]
    public async Task Pending_approval_alerts_use_their_own_flag()
    {
        var rig = new Rig();
        await rig.Seed(Staff("s1"), Staff("s2"),
            new AdminNotificationPreference { InstitutionId = Inst, StaffId = "s1", PendingApprovalAlerts = false, PaymentReceivedAlerts = true });

        Assert.Equal(new[] { "s2" }, await rig.Activities.ResolvePendingApprovalAdminRecipientsAsync(Inst));
        Assert.Equal(2, (await rig.Activities.ResolvePaymentReceivedAdminRecipientsAsync(Inst)).Count);
    }

    [Fact]
    public async Task Staff_of_other_institutions_are_never_alerted()
    {
        var rig = new Rig();
        var other = Staff("s9"); other.InstitutionId = "other";
        await rig.Seed(Staff("s1"), other);
        Assert.Equal(new[] { "s1" }, await rig.Activities.ResolvePaymentReceivedAdminRecipientsAsync(Inst));
    }

    // ── Single recipient ────────────────────────────────────────────────

    [Fact]
    public async Task A_member_is_loaded_with_their_preferences_and_sensible_defaults()
    {
        var rig = new Rig();
        await rig.Seed(Member("a"), Member("b"),
            new NotificationPreference { InstitutionId = Inst, MemberId = "a", SmsAlerts = true, WhatsAppAlerts = true, EventReminders = false });

        var a = (await rig.Activities.LoadMemberWithPreferenceAsync("a"))!;
        var b = (await rig.Activities.LoadMemberWithPreferenceAsync("b"))!;

        Assert.Equal(("a@x.com", true, true, false), (a.Email, a.SmsAlerts, a.WhatsAppAlerts, a.EventReminders));
        Assert.Equal((false, false, true), (b.SmsAlerts, b.WhatsAppAlerts, b.EventReminders));
        Assert.Null(await rig.Activities.LoadMemberWithPreferenceAsync("ghost"));
    }

    // ── In-app notifications ────────────────────────────────────────────

    private static Notification Note(string recipient = "m1", string type = "JobAlert", string related = "j1", string body = "New job") =>
        new() { RecipientId = recipient, RecipientType = "Member", Type = type, RelatedEntityId = related, Body = body, Title = "T" };

    [Fact]
    public async Task A_notification_is_saved_under_the_institution()
    {
        var rig = new Rig();
        await rig.Activities.CreateNotificationAsync(Inst, Note());

        var saved = await rig.Db().Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((Inst, "m1"), (saved.InstitutionId, saved.RecipientId));
    }

    [Fact]
    public async Task An_identical_notification_is_not_saved_twice_so_activity_retries_are_safe()
    {
        var rig = new Rig();
        await rig.Activities.CreateNotificationAsync(Inst, Note());
        rig.Fresh();
        await rig.Activities.CreateNotificationAsync(Inst, Note());

        Assert.Equal(1, await rig.Db().Notifications.IgnoreQueryFilters().CountAsync());
    }

    [Theory]
    [InlineData("m2", "JobAlert", "j1", "New job")]
    [InlineData("m1", "CampaignAlert", "j1", "New job")]
    [InlineData("m1", "JobAlert", "j2", "New job")]
    [InlineData("m1", "JobAlert", "j1", "A different body")]
    public async Task A_notification_differing_in_any_identity_field_is_a_new_one(string recipient, string type, string related, string body)
    {
        var rig = new Rig();
        await rig.Activities.CreateNotificationAsync(Inst, Note());
        rig.Fresh();
        await rig.Activities.CreateNotificationAsync(Inst, Note(recipient, type, related, body));

        Assert.Equal(2, await rig.Db().Notifications.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task The_same_notification_for_a_different_institution_is_not_a_duplicate()
    {
        var rig = new Rig();
        await rig.Activities.CreateNotificationAsync("inst-a", Note());
        rig.Fresh();
        await rig.Activities.CreateNotificationAsync("inst-b", Note());
        Assert.Equal(2, await rig.Db().Notifications.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Batch_creation_saves_only_the_new_ones_and_skips_duplicates_already_stored()
    {
        var rig = new Rig();
        await rig.Activities.CreateNotificationAsync(Inst, Note("m1"));
        rig.Fresh();

        await rig.Activities.CreateNotificationsAsync(Inst, [Note("m1"), Note("m2"), Note("m3")]);

        var recipients = await rig.Db().Notifications.IgnoreQueryFilters().Select(n => n.RecipientId).ToListAsync();
        Assert.Equal(new[] { "m1", "m2", "m3" }, recipients.OrderBy(x => x));
    }

    [Fact]
    public async Task Batch_creation_with_nothing_new_writes_nothing()
    {
        var rig = new Rig();
        await rig.Activities.CreateNotificationAsync(Inst, Note("m1"));
        rig.Fresh();
        await rig.Activities.CreateNotificationsAsync(Inst, [Note("m1")]);
        await rig.Activities.CreateNotificationsAsync(Inst, []);
        Assert.Equal(1, await rig.Db().Notifications.IgnoreQueryFilters().CountAsync());
    }

    // ── Channels ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sms_and_whatsapp_report_the_gateways_success_flag_so_retries_can_trigger(bool ok)
    {
        var rig = new Rig();
        rig.Sms.Setup(s => s.SendSmsAsync("0241", "hi", It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        rig.WhatsApp.Setup(w => w.SendMessageAsync("0241", "hi", It.IsAny<CancellationToken>())).ReturnsAsync(ok);

        Assert.Equal(ok, await rig.Activities.SendSmsAsync("0241", "hi"));
        Assert.Equal(ok, await rig.Activities.SendWhatsAppAsync("0241", "hi"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Email_reports_the_providers_success_flag(bool ok)
    {
        var rig = new Rig();
        rig.Email.Setup(e => e.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailtrapResponse<MailtrapSendMessageResponse> { Success = ok });

        Assert.Equal(ok, await rig.Activities.SendEmailAsync(new SendEmailRequest { TemplateId = "welcome" }, "welcome"));
    }

    [Fact]
    public async Task A_gateway_that_throws_surfaces_as_a_temporal_application_failure()
    {
        var rig = new Rig();
        rig.Sms.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() => rig.Activities.SendSmsAsync("0241", "hi"));

        Assert.Contains("send SMS", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    // ── Web push ────────────────────────────────────────────────────────

    private static PushSubscriptionEntity Sub(string endpoint, string owner = "m1", string type = "Member", bool active = true) =>
        new() { Id = endpoint, InstitutionId = Inst, OwnerId = owner, OwnerType = type, Endpoint = endpoint, P256dhKey = "p", AuthKey = "a", IsActive = active };

    [Fact]
    public async Task Push_returns_false_without_calling_the_gateway_when_the_owner_has_no_active_subscriptions()
    {
        var rig = new Rig();
        await rig.Seed(Sub("old", active: false), Sub("other", owner: "m2"), Sub("staff", type: "InstitutionStaff"));

        Assert.False(await rig.Activities.SendWebPushAsync("m1", "Member", "T", "B", "/x"));
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Push_sends_to_every_device_and_stamps_last_used_on_success()
    {
        var rig = new Rig();
        await rig.Seed(Sub("laptop"), Sub("phone"));
        rig.Push.Setup(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), "Title", "Body", "/go", It.IsAny<CancellationToken>())).ReturnsAsync(WebPushSendResult.Sent);

        var sent = await rig.Activities.SendWebPushAsync("m1", "Member", "Title", "Body", "/go");

        Assert.True(sent);
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), "Title", "Body", "/go", It.IsAny<CancellationToken>()), Times.Exactly(2));
        var subs = await rig.Db().PushSubscriptions.IgnoreQueryFilters().ToListAsync();
        Assert.All(subs, s => { Assert.NotNull(s.LastUsedAt); Assert.True(s.IsActive); });
    }

    [Fact]
    public async Task A_subscription_the_push_service_reports_gone_is_deactivated_and_the_others_still_get_the_message()
    {
        var rig = new Rig();
        await rig.Seed(Sub("dead"), Sub("alive"));
        rig.Push.Setup(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint == "dead"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(WebPushSendResult.Gone);
        rig.Push.Setup(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint == "alive"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(WebPushSendResult.Sent);

        Assert.True(await rig.Activities.SendWebPushAsync("m1", "Member", "T", "B", null));

        var subs = await rig.Db().PushSubscriptions.IgnoreQueryFilters().ToDictionaryAsync(s => s.Endpoint);
        Assert.False(subs["dead"].IsActive);
        Assert.NotNull(subs["dead"].LastFailedAt);
        Assert.True(subs["alive"].IsActive);
    }

    [Fact]
    public async Task A_transient_failure_records_the_failure_but_keeps_the_subscription_and_reports_not_sent()
    {
        var rig = new Rig();
        await rig.Seed(Sub("flaky"));
        rig.Push.Setup(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(WebPushSendResult.TransientFailure);

        Assert.False(await rig.Activities.SendWebPushAsync("m1", "Member", "T", "B", null));

        var sub = await rig.Db().PushSubscriptions.IgnoreQueryFilters().SingleAsync();
        Assert.True(sub.IsActive);
        Assert.NotNull(sub.LastFailedAt);
        Assert.Null(sub.LastUsedAt);
    }

    [Fact]
    public async Task Staff_and_member_subscriptions_with_the_same_id_are_kept_apart_by_owner_type()
    {
        var rig = new Rig();
        await rig.Seed(Sub("member-device", owner: "same-id", type: "Member"), Sub("staff-device", owner: "same-id", type: "InstitutionStaff"));
        rig.Push.Setup(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(WebPushSendResult.Sent);

        await rig.Activities.SendWebPushAsync("same-id", "InstitutionStaff", "T", "B", null);

        rig.Push.Verify(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint == "staff-device"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        rig.Push.Verify(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint == "member-device"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

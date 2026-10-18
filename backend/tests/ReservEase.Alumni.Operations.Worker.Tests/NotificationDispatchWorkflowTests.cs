using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Operations.Worker.Workflows.Notifications;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.WebPush.Sdk.Services;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using Temporalio.Client;
using Temporalio.Worker;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using PushSubscriptionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.PushSubscription;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class NotificationDispatchWorkflowTests(TemporalFixture temporal)
{
    private const string Inst = "i1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<ISmsService> Sms { get; } = new();
        public Mock<IWhatsAppService> WhatsApp { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public Mock<IWebPushService> Push { get; } = new();
        public List<string> SmsSent { get; } = new();
        public List<SendEmailRequest> EmailsSent { get; } = new();
        public NotificationDispatchActivities Activities { get; }

        public Rig(Action<Institution>? institution = null, bool whatsAppOn = false, ReservEase.Alumni.Operations.Worker.Services.IWhatsAppDeliveryPolicy? policy = null, bool botOn = true)
        {
            Sms.Setup(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((p, m, _) => SmsSent.Add($"{p}|{m}")).ReturnsAsync(true);
            WhatsApp.Setup(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Email.Setup(e => e.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
                .Callback<SendEmailRequest, CancellationToken>((r, _) => EmailsSent.Add(r))
                .ReturnsAsync(new MailtrapResponse<MailtrapSendMessageResponse> { Success = true });
            Push.Setup(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WebPushSendResult.Sent);

            var inst = new Institution { Id = Inst, Name = "UMaT", PortalName = "UMaT Portal", Slug = "umat" };
            institution?.Invoke(inst);
            using (var seed = TestDb.Create(DbName)) { seed.Institutions.Add(inst); seed.SaveChanges(); }

            var tenant = TestDb.Tenant(null);
            var db = TestDb.Create(DbName, tenant: tenant);
            Activities = new NotificationDispatchActivities(
                new AlumniPgRepository<Notification>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<NotificationPreference>(db),
                new AlumniPgRepository<AdminNotificationPreference>(db), new AlumniPgRepository<StaffEntity>(db), new AlumniPgRepository<Institution>(db),
                new AlumniPgRepository<Job>(db), new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<AlumniEvent>(db),
                new AlumniPgRepository<Spotlight>(db), new AlumniPgRepository<ClassNote>(db), new AlumniPgRepository<PushSubscriptionEntity>(db),
                tenant, Sms.Object, WhatsApp.Object, Email.Object, Push.Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MemberPortalBaseDomain"] = "members.test", ["AdminPortalBaseDomain"] = "admin.test",
                    ["WaSenderConfig:Enabled"] = whatsAppOn ? "true" : "false",
                }).Build(),
                NullLogger<NotificationDispatchActivities>.Instance, policy,
                Mock.Of<ReservEase.Alumni.PostgresDb.Sdk.Services.IPlatformWhatsAppSwitch>(x => x.IsOnAsync() == Task.FromResult(botOn)));
        }

        public AlumniDbContext Db() => TestDb.Create(DbName);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) db.Add(e);
            await db.SaveChangesAsync();
        }

        public async Task<List<Notification>> Notes() => await Db().Notifications.IgnoreQueryFilters().OrderBy(n => n.RecipientId).ToListAsync();
    }

    private async Task Send(Rig rig, NotificationRequest request)
    {
        var queue = "q-" + Guid.NewGuid().ToString("N");
        using var worker = new TemporalWorker(temporal.Client,
            new TemporalWorkerOptions(queue).AddWorkflow<NotificationDispatchWorkflow>().AddAllActivities(rig.Activities));
        await worker.ExecuteAsync(() => temporal.Client.ExecuteWorkflowAsync(
            (NotificationDispatchWorkflow wf) => wf.RunAsync(request), new WorkflowOptions("wf-" + Guid.NewGuid().ToString("N"), queue)));
    }

    private static MemberEntity Person(string id, int year = 2015, string status = "Active", string? phone = "0241000000") =>
        new() { Id = id, InstitutionId = Inst, Status = status, GraduationYear = year, Phone = phone, Email = $"{id}@x.com", FirstName = id };

    private static NotificationPreference Pref(string memberId, bool sms = false, bool eventReminders = true) =>
        new() { InstitutionId = Inst, MemberId = memberId, SmsAlerts = sms, EventReminders = eventReminders };

    private static PushSubscriptionEntity Device(string owner, string type = "Member") =>
        new() { Id = $"dev-{owner}-{type}", InstitutionId = Inst, OwnerId = owner, OwnerType = type, Endpoint = $"https://push/{owner}/{type}", P256dhKey = "p", AuthKey = "a", IsActive = true };

    // ── Fan-out alerts ──────────────────────────────────────────────────

    [WorkflowFact]
    public async Task A_job_alert_creates_one_in_app_notification_per_eligible_member_with_a_deep_link()
    {
        var rig = new Rig();
        await rig.Seed(Person("a"), Person("b"), Pref("b"), new Job { Id = "j1", Title = "Engineer", Company = "Acme", Location = "Accra" });

        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));

        var notes = await rig.Notes();
        Assert.Equal(new[] { "a", "b" }, notes.Select(n => n.RecipientId));
        Assert.All(notes, n =>
        {
            Assert.Equal(("New Job Posting", "Engineer at Acme — Accra", "JobAlert", "Member"), (n.Title, n.Body, n.Type, n.RecipientType));
            Assert.Equal(("j1", "Job", "https://umat.members.test/jobs/j1", "system"), (n.RelatedEntityId, n.RelatedEntityType, n.ActionUrl, n.CreatedBy));
        });
    }

    [WorkflowFact]
    public async Task A_job_alert_for_a_missing_job_or_with_nobody_to_tell_does_nothing()
    {
        var rig = new Rig();
        await rig.Seed(Person("a"), new Job { Id = "j1", Title = "T", Company = "C", Location = "L", YearGroups = [1999] });

        await Send(rig, NotificationRequest.JobAlert(Inst, "ghost"));
        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));   // nobody graduated in 1999

        Assert.Empty(await rig.Notes());
    }

    [WorkflowFact]
    public async Task Opted_out_members_get_neither_the_in_app_row_nor_the_push()
    {
        var rig = new Rig();
        await rig.Seed(Person("a"), Person("b"), new NotificationPreference { InstitutionId = Inst, MemberId = "b", JobAlerts = false },
            Device("a"), Device("b"), new Job { Id = "j1", Title = "T", Company = "C", Location = "L" });

        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));

        Assert.Equal(new[] { "a" }, (await rig.Notes()).Select(n => n.RecipientId));
        rig.Push.Verify(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint.Contains("/a/")), "New Job Posting", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        rig.Push.Verify(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint.Contains("/b/")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [WorkflowFact]
    public async Task Campaign_event_and_spotlight_alerts_have_their_own_titles_bodies_and_links()
    {
        var rig = new Rig();
        await rig.Seed(Person("a"),
            new Campaign { Id = "c1", Title = "Library fund" },
            new AlumniEvent { Id = "e1", Title = "Gala", Venue = "Main Hall", StartDate = new DateTime(2026, 12, 5, 18, 0, 0, DateTimeKind.Utc) },
            new Spotlight { Id = "s1", Title = "Builds bridges", Member = new MemberSnapshot { FirstName = "Ama", LastName = "Mensah" } });

        await Send(rig, NotificationRequest.CampaignAlert(Inst, "c1"));
        await Send(rig, NotificationRequest.EventReminder(Inst, "e1"));
        await Send(rig, NotificationRequest.SpotlightAlert(Inst, "s1"));

        var notes = (await rig.Notes()).ToDictionary(n => n.Type);
        Assert.Equal(("New Campaign Launched", "Library fund", "https://umat.members.test/contributions"), (notes["CampaignAlert"].Title, notes["CampaignAlert"].Body, notes["CampaignAlert"].ActionUrl));
        Assert.Equal(("Upcoming Event", "Gala — Saturday, December 5 2026 at Main Hall", "https://umat.members.test/events/e1"), (notes["EventReminder"].Title, notes["EventReminder"].Body, notes["EventReminder"].ActionUrl));
        Assert.Equal(("New Spotlight", "Ama Mensah — Builds bridges", "https://umat.members.test/spotlights"), (notes["SpotlightUpdate"].Title, notes["SpotlightUpdate"].Body, notes["SpotlightUpdate"].ActionUrl));
    }

    [WorkflowFact]
    public async Task A_spotlight_without_a_member_name_is_credited_to_a_member()
    {
        var rig = new Rig();
        await rig.Seed(Person("a"), new Spotlight { Id = "s1", Title = "Hidden hero" });

        await Send(rig, NotificationRequest.SpotlightAlert(Inst, "s1"));

        Assert.Equal("A member — Hidden hero", (await rig.Notes()).Single().Body);
    }

    [WorkflowFact]
    public async Task Without_a_resolvable_institution_the_action_link_is_empty_rather_than_a_dev_url()
    {
        var noDomain = new Rig();
        // With the institution row gone no portal link can be built at all.
        await noDomain.Seed(Person("a"), new Job { Id = "j1", Title = "T", Company = "C", Location = "L" });
        using (var db = noDomain.Db()) { db.Institutions.RemoveRange(db.Institutions); await db.SaveChangesAsync(); }

        await Send(noDomain, NotificationRequest.JobAlert(Inst, "j1"));

        Assert.Equal(string.Empty, (await noDomain.Notes()).Single().ActionUrl);
    }

    // ── Admin alerts ────────────────────────────────────────────────────

    [WorkflowFact]
    public async Task A_submitted_payment_alerts_enabled_admins_with_the_amount_formatted_and_an_admin_link()
    {
        var rig = new Rig();
        await rig.Seed(new StaffEntity { Id = "s1", InstitutionId = Inst }, new StaffEntity { Id = "s2", InstitutionId = Inst, IsDisabled = true }, Device("s1", "InstitutionStaff"));

        await Send(rig, NotificationRequest.PaymentReceivedToAdmins(Inst, "Ama Mensah", "ama@x.com", 1234.5m, "Library fund", "con-1"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("s1", "Admin", "Payment Submitted", "PaymentReceived"), (n.RecipientId, n.RecipientType, n.Title, n.Type));
        Assert.Equal("Ama Mensah (ama@x.com) submitted a payment of GHS 1,234.50 for the campaign \"Library fund\". Please review and confirm.", n.Body);
        Assert.Equal(("con-1", "https://umat.admin.test/contributions"), (n.RelatedEntityId, n.ActionUrl));
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), "Payment Submitted", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [WorkflowFact]
    public async Task A_new_member_awaiting_approval_alerts_admins()
    {
        var rig = new Rig();
        await rig.Seed(new StaffEntity { Id = "s1", InstitutionId = Inst });

        await Send(rig, NotificationRequest.NewMemberPendingApproval(Inst, "m9", "Kofi Boateng", "kofi@x.com"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("New Member Awaiting Approval", "Kofi Boateng (kofi@x.com) registered and is waiting for approval.", "https://umat.admin.test/members"), (n.Title, n.Body, n.ActionUrl));
    }

    [WorkflowFact]
    public async Task A_member_request_alerts_full_administrators_only_and_points_at_the_page_that_handles_it()
    {
        var rig = new Rig();
        await rig.Seed(new StaffEntity { Id = "boss", InstitutionId = Inst, Role = "SuperAdmin" }, new StaffEntity { Id = "scoped", InstitutionId = Inst, Role = "ScopedAdmin" },
            new StaffEntity { Id = "off", InstitutionId = Inst, Role = "SuperAdmin", IsDisabled = true }, Device("boss", "InstitutionStaff"));

        await Send(rig, NotificationRequest.RequestAwaitingReview(Inst, "BusinessListingSubmitted", "Business listing to review", "Ama submitted \"Kofi Bakes\" on 9 Oct 2026.", "BusinessListing", "b1", "/business-directory?status=Pending"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("boss", "Admin", "Business listing to review", "BusinessListingSubmitted"), (n.RecipientId, n.RecipientType, n.Title, n.Type));
        Assert.Equal(("b1", "BusinessListing", "https://umat.admin.test/business-directory?status=Pending"), (n.RelatedEntityId, n.RelatedEntityType, n.ActionUrl));
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), "Business listing to review", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [WorkflowFact]
    public async Task The_same_request_is_not_alerted_twice_but_a_later_one_about_the_same_item_is()
    {
        var rig = new Rig();
        await rig.Seed(new StaffEntity { Id = "boss", InstitutionId = Inst, Role = "SuperAdmin" });
        NotificationRequest Req(string day) => NotificationRequest.RequestAwaitingReview(Inst, "BusinessListingChanged", "Changes to review", $"Ama changed \"Kofi Bakes\" on {day}.", "BusinessListing", "b1", "/business-directory?status=Pending");

        await Send(rig, Req("9 Oct 2026"));
        await Send(rig, Req("9 Oct 2026"));
        Assert.Single(await rig.Notes());

        await Send(rig, Req("12 Oct 2026"));
        Assert.Equal(2, (await rig.Notes()).Count);
    }

    [WorkflowFact]
    public async Task Admin_alerts_with_no_enabled_staff_do_nothing()
    {
        var rig = new Rig();
        await rig.Seed(new StaffEntity { Id = "s1", InstitutionId = Inst, IsDisabled = true });
        await Send(rig, NotificationRequest.PaymentReceivedToAdmins(Inst, "A", "a@x.com", 1, "C", "x"));
        await Send(rig, NotificationRequest.NewMemberPendingApproval(Inst, "m", "A", "a@x.com"));
        Assert.Empty(await rig.Notes());
    }

    // ── Personal notifications ──────────────────────────────────────────

    [WorkflowFact]
    public async Task A_confirmed_contribution_thanks_the_member_by_name_in_app_and_by_sms_when_opted_in()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1", phone: "0241234567"), Pref("m1", sms: true));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 50m, "Library fund", "con-1"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal("Thank you, Ama — your payment of GHS 50.00 for \"Library fund\" has been received.", n.Body);
        Assert.Equal(("Contribution Confirmed", "https://umat.members.test/contributions"), (n.Title, n.ActionUrl));
        var sms = Assert.Single(rig.SmsSent);
        Assert.StartsWith("0241234567|UMaT: Thank you, Ama - your payment of GHS 50.00", sms);   // prefixed with the institution, em dash made SMS-safe
        Assert.EndsWith("https://umat.members.test/contributions", sms);
        Assert.DoesNotContain("—", sms);
    }

    [WorkflowFact]
    public async Task A_confirmed_contribution_without_a_first_name_uses_the_generic_thanks()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"));
        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "", 5m, "Fund", "c"));
        Assert.Equal("Your payment of GHS 5.00 for \"Fund\" has been received. Thank you!", (await rig.Notes()).Single().Body);
    }

    [WorkflowTheory]
    [InlineData("opted-out")]
    [InlineData("no-phone")]
    [InlineData("institution-sms-off")]
    public async Task No_sms_goes_out_unless_the_member_opted_in_has_a_phone_and_the_institution_allows_it(string scenario)
    {
        var rig = new Rig(i => i.SmsNotificationsEnabled = scenario != "institution-sms-off");
        await rig.Seed(Person("m1", phone: scenario == "no-phone" ? null : "0241"), Pref("m1", sms: scenario != "opted-out"));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        Assert.Empty(rig.SmsSent);
        Assert.Single(await rig.Notes());   // the in-app notification is unaffected
    }

    [WorkflowFact]
    public async Task Whatsapp_is_never_sent_while_the_channel_is_switched_off()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), new NotificationPreference { InstitutionId = Inst, MemberId = "m1", SmsAlerts = true, WhatsAppAlerts = true });
        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));
        rig.WhatsApp.Verify(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── WhatsApp first, SMS as the fallback ───────────────────────────────

    private static MemberEntity Verified(string id) { var m = Person(id); m.PhoneVerifiedAt = DateTime.UtcNow; return m; }
    private static NotificationPreference Both(string id, bool whatsApp = true) =>
        new() { InstitutionId = Inst, MemberId = id, SmsAlerts = true, WhatsAppAlerts = whatsApp };

    [WorkflowFact]
    public async Task A_verified_consenting_member_gets_whatsapp_with_the_link_and_no_sms()
    {
        var rig = new Rig(whatsAppOn: true);
        await rig.Seed(Verified("m1"), Both("m1"));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync("0241000000",
            It.Is<string>(t => t.StartsWith("*UMaT*\n") && t.Contains("Thank you, Ama") && t.EndsWith("https://umat.members.test/contributions")), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task When_whatsapp_cannot_deliver_the_sms_still_goes_out()
    {
        var rig = new Rig(whatsAppOn: true);
        rig.WhatsApp.Setup(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await rig.Seed(Verified("m1"), Both("m1"));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(rig.SmsSent);
    }

    private static MessagingUsageMonth ThisMonth(Rig rig)
    {
        using var db = rig.Db();
        db.Institutions.Single(i => i.Id == Inst).MessagingUsage.TryGetValue(MessagingUsageKeys.For(DateTime.UtcNow), out var m);
        return m ?? new MessagingUsageMonth();
    }

    [WorkflowFact]
    public async Task The_months_tally_counts_a_whatsapp_delivery_as_a_message_not_sent_by_sms()
    {
        var rig = new Rig(whatsAppOn: true);
        await rig.Seed(Verified("m1"), Both("m1"));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        var usage = ThisMonth(rig);
        Assert.Equal((1, 0, 0), (usage.WhatsAppDelivered, usage.SmsSent, usage.SmsAfterWhatsAppFailure));
    }

    [WorkflowFact]
    public async Task The_tally_separates_sms_that_went_only_because_whatsapp_failed()
    {
        var rig = new Rig(whatsAppOn: true);
        rig.WhatsApp.Setup(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await rig.Seed(Verified("m1"), Both("m1"));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        var usage = ThisMonth(rig);
        Assert.Equal((0, 1, 1), (usage.WhatsAppDelivered, usage.SmsSent, usage.SmsAfterWhatsAppFailure));
    }

    [WorkflowFact]
    public async Task A_plain_sms_is_counted_as_sms_with_no_failure_attached()
    {
        var rig = new Rig(whatsAppOn: true);
        await rig.Seed(Person("m1"), Both("m1"));   // number never proved: SMS only

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        var usage = ThisMonth(rig);
        Assert.Equal((0, 1, 0), (usage.WhatsAppDelivered, usage.SmsSent, usage.SmsAfterWhatsAppFailure));
    }

    [WorkflowFact]
    public async Task A_welcome_in_app_message_mentions_whatsapp_only_when_the_channel_is_open()
    {
        var on = new Rig(whatsAppOn: true);
        await on.Seed(Person("m1"), Both("m1"));
        await Send(on, NotificationRequest.MemberStatusChanged(Inst, "m1", "Ama", "Active", null));
        using (var db = on.Db()) Assert.Contains("Connect WhatsApp", db.Set<Notification>().IgnoreQueryFilters().Single(n => n.RecipientId == "m1").Body);

        var off = new Rig(whatsAppOn: false);
        await off.Seed(Person("m1"), Both("m1"));
        await Send(off, NotificationRequest.MemberStatusChanged(Inst, "m1", "Ama", "Active", null));
        using (var db = off.Db()) Assert.DoesNotContain("WhatsApp", db.Set<Notification>().IgnoreQueryFilters().Single(n => n.RecipientId == "m1").Body);
    }

    // ── Email that prefers WhatsApp ──────────────────────────────────────

    private static NotificationRequest ReminderEmail(string memberId, string priority = WhatsAppPriority.Reminder)
        => NotificationRequest.Email(
            new SendEmailRequest { To = [new EmailContact { Email = $"{memberId}@x.com", Name = memberId }], TemplateId = "notification" },
            "test reminder",
            new WhatsAppAlternative { InstitutionId = Inst, MemberId = memberId, Text = "Your dues are due.", ActionUrl = "https://umat.members.test/contributions", Priority = priority });

    [WorkflowFact]
    public async Task A_reminder_goes_to_whatsapp_instead_of_email_for_a_member_who_connected_it()
    {
        var rig = new Rig(whatsAppOn: true);
        await rig.Seed(Verified("m1"), Both("m1"));

        await Send(rig, ReminderEmail("m1"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync("0241000000",
            It.Is<string>(t => t.StartsWith("*UMaT*\n") && t.Contains("Your dues are due.") && t.EndsWith("https://umat.members.test/contributions")), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(rig.EmailsSent);
        Assert.Empty(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task With_the_bot_switched_off_by_the_platform_everything_falls_back_to_sms_and_email()
    {
        var rig = new Rig(whatsAppOn: true, botOn: false);
        await rig.Seed(Verified("m1"), Both("m1"), new Job { Id = "j1", Title = "Engineer", Company = "Acme", Location = "Accra" });

        // A receipt: WhatsApp would have carried it; now SMS does.
        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));
        // A reminder: WhatsApp would have carried it instead of email; now the email goes.
        await Send(rig, ReminderEmail("m1"));
        // News: nothing on WhatsApp.
        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(rig.SmsSent);
        Assert.Single(rig.EmailsSent, e => e.To![0].Email == "m1@x.com");
        Assert.Single(await rig.Notes(), n => n.Type == "JobAlert");   // the in-app notice is unaffected
    }

    [WorkflowTheory]
    [InlineData("not-connected")]
    [InlineData("no-consent")]
    [InlineData("whatsapp-fails")]
    [InlineData("channel-off")]
    public async Task A_reminder_still_goes_by_email_when_whatsapp_cannot_carry_it(string scenario)
    {
        var rig = new Rig(whatsAppOn: scenario != "channel-off");
        if (scenario == "whatsapp-fails")
            rig.WhatsApp.Setup(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await rig.Seed(scenario == "not-connected" ? Person("m1") : Verified("m1"), Both("m1", whatsApp: scenario != "no-consent"));

        await Send(rig, ReminderEmail("m1"));

        Assert.Single(rig.EmailsSent);
        Assert.Empty(rig.SmsSent);   // a reminder never turns into an SMS
    }

    [WorkflowFact]
    public async Task A_reminder_held_back_by_quiet_hours_or_the_daily_limit_goes_by_email()
    {
        var policy = new Mock<ReservEase.Alumni.Operations.Worker.Services.IWhatsAppDeliveryPolicy>();
        policy.Setup(p => p.TryReserveAsync(It.IsAny<string>(), WhatsAppPriority.Reminder)).ReturnsAsync(false);
        var rig = new Rig(whatsAppOn: true, policy: policy.Object);
        await rig.Seed(Verified("m1"), Both("m1"));

        await Send(rig, ReminderEmail("m1"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(rig.EmailsSent);
    }

    [WorkflowFact]
    public async Task A_new_job_is_told_on_whatsapp_to_connected_members_only_and_never_by_sms()
    {
        var rig = new Rig(whatsAppOn: true);
        var connected = Verified("a");
        await rig.Seed(connected, Person("b"), Both("a"), Both("b"), new Job { Id = "j1", Title = "Engineer", Company = "Acme", Location = "Accra" });

        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync("0241000000", It.Is<string>(t => t.Contains("New job: Engineer at Acme, Accra.") && t.EndsWith("/jobs/j1")), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(rig.SmsSent);
        Assert.Equal(new[] { "a", "b" }, (await rig.Notes()).Select(n => n.RecipientId));   // both still get the in-app notice
    }

    [WorkflowFact]
    public async Task A_mentorship_decision_is_a_receipt_and_goes_to_whatsapp_even_during_quiet_hours()
    {
        var policy = new Mock<ReservEase.Alumni.Operations.Worker.Services.IWhatsAppDeliveryPolicy>();
        policy.Setup(p => p.TryReserveAsync(It.IsAny<string>(), WhatsAppPriority.Transactional)).ReturnsAsync(true);
        policy.Setup(p => p.TryReserveAsync(It.IsAny<string>(), WhatsAppPriority.Update)).ReturnsAsync(false);
        var rig = new Rig(whatsAppOn: true, policy: policy.Object);
        await rig.Seed(Verified("mentee"), Both("mentee"));

        await Send(rig, NotificationRequest.MentorshipRequestDecision(Inst, "mentee", true, "Career", "req-1"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync("0241000000", It.Is<string>(t => t.Contains("accepted")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [WorkflowTheory]
    [InlineData("unverified-number")]
    [InlineData("no-whatsapp-consent")]
    [InlineData("institution-turned-it-off")]
    [InlineData("deactivated-for-this-institution-by-the-platform")]
    [InlineData("platform-off")]
    public async Task Whatsapp_is_not_used_unless_every_condition_holds_and_sms_carries_on_as_before(string scenario)
    {
        var rig = new Rig(i =>
        {
            if (scenario == "institution-turned-it-off") i.DisabledFeatures = [InstitutionFeatures.WhatsApp];
            if (scenario == "deactivated-for-this-institution-by-the-platform") i.WhatsAppBotEnabled = false;
        }, whatsAppOn: scenario != "platform-off");
        var member = scenario == "unverified-number" ? Person("m1") : Verified("m1");
        await rig.Seed(member, Both("m1", whatsApp: scenario != "no-whatsapp-consent"));

        await Send(rig, NotificationRequest.ContributionConfirmed(Inst, "m1", "m1@x.com", "Ama", 5m, "Fund", "c"));

        rig.WhatsApp.Verify(w => w.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task A_rejected_contribution_explains_the_reason_when_there_is_one()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"));

        await Send(rig, NotificationRequest.ContributionRejected(Inst, "m1", "m1@x.com", "Ama", "Fund", "Duplicate proof", "c1"));
        await Send(rig, NotificationRequest.ContributionRejected(Inst, "m1", "m1@x.com", "Ama", "Fund", null, "c2"));

        var bodies = (await rig.Notes()).Select(n => n.Body).OrderBy(b => b).ToList();
        Assert.Contains("Your contribution for \"Fund\" was not confirmed. Please contact support or resubmit.", bodies);
        Assert.Contains("Your contribution for \"Fund\" was not confirmed. Reason: Duplicate proof. Please resubmit or contact support.", bodies);
    }

    [WorkflowTheory]
    [InlineData("Active", true, "Welcome — You're Approved!", "Welcome, Ama — your membership has been approved. You now have full access to the portal.")]
    [InlineData("Reinstated", true, "Account Reinstated", "Good news, Ama — your account has been reinstated. You can log back in now.")]
    [InlineData("Suspended", false, "Registration Not Approved", "Your registration was not approved. Reason: Incomplete details. You're welcome to register again.")]
    [InlineData("Banned", false, "Account Suspended", "Your account has been suspended. Reason: Incomplete details.")]
    [InlineData("Blocked", false, "Registration Blocked", "Your registration was not approved after multiple attempts, and this email address can no longer be used to register.")]
    [InlineData("Unknown", false, "Account Status Updated", "Your account status has been updated.")]
    public async Task A_status_change_has_the_right_copy_and_only_members_who_can_sign_in_get_an_in_app_row(string status, bool inApp, string title, string body)
    {
        var rig = new Rig();
        await rig.Seed(Person("m1", phone: "0241"), Pref("m1", sms: true));

        await Send(rig, NotificationRequest.MemberStatusChanged(Inst, "m1", "Ama", status, "Incomplete details"));

        var notes = await rig.Notes();
        Assert.Equal(inApp ? 1 : 0, notes.Count);
        if (inApp) Assert.Equal((title, body), (notes[0].Title, notes[0].Body));
        // Everyone who opted in is still reached by SMS, signed in or not.
        Assert.Contains(rig.SmsSent, s => s.Contains(body.Replace("—", "-")));
    }

    [WorkflowFact]
    public async Task Status_change_copy_without_a_reason_or_first_name_falls_back_gracefully()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"));

        await Send(rig, NotificationRequest.MemberStatusChanged(Inst, "m1", "", "Active", null));
        await Send(rig, NotificationRequest.MemberStatusChanged(Inst, "m1", "", "Suspended", null));
        await Send(rig, NotificationRequest.MemberStatusChanged(Inst, "m1", "", "Banned", null));

        Assert.Empty(rig.SmsSent);   // nobody opted in
        var active = Assert.Single(await rig.Notes());
        Assert.Equal("Your membership has been approved. Welcome aboard!", active.Body);
    }

    [WorkflowTheory]
    [InlineData("mentor-approved")]
    [InlineData("mentor-declined")]
    [InlineData("store")]
    [InlineData("service")]
    [InlineData("mentorship-received")]
    [InlineData("mentorship-accepted")]
    [InlineData("mentorship-declined")]
    [InlineData("forum")]
    [InlineData("referral")]
    [InlineData("rsvp")]
    [InlineData("spotlight-approved")]
    [InlineData("spotlight-declined")]
    [InlineData("spotlight-declined-reason")]
    public async Task Single_recipient_notifications_have_the_expected_title_body_type_and_link(string which)
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"));
        var (request, title, body, type, link) = which switch
        {
            "mentor-approved" => (NotificationRequest.MentorProfileDecision(Inst, "m1", true, "p1"), "Mentor Application Approved", "Congratulations — your mentor application has been approved. You can now receive mentorship requests.", "MentorProfileDecision", "/mentorship"),
            "mentor-declined" => (NotificationRequest.MentorProfileDecision(Inst, "m1", false, "p1"), "Mentor Application Not Approved", "Your mentor application was not approved this time.", "MentorProfileDecision", "/mentorship"),
            "store" => (NotificationRequest.StoreDeliveryStatusUpdated(Inst, "m1", "o1", "ORD-9", "Shipped"), "Order Delivery Update", "Your order #ORD-9 is now \"Shipped\".", "StoreDeliveryStatusUpdated", "/store/orders"),
            "service" => (NotificationRequest.ServiceRequestUpdated(Inst, "m1", "r1", "REQ-3", "Transcript", "Processing"), "Service Request Update", "Your \"Transcript\" request #REQ-3 is now \"Processing\".", "ServiceRequestUpdated", "/services/requests"),
            "mentorship-received" => (NotificationRequest.MentorshipRequestReceived(Inst, "m1", "Esi", "Engineering", "r1"), "New Mentorship Request", "Esi has requested mentorship from you in Engineering.", "MentorshipRequestReceived", "/mentorship"),
            "mentorship-accepted" => (NotificationRequest.MentorshipRequestDecision(Inst, "m1", true, "Engineering", "r1"), "Mentorship Request Accepted", "Your mentorship request in Engineering was accepted. You can now connect with your mentor.", "MentorshipRequestDecision", "/mentorship"),
            "mentorship-declined" => (NotificationRequest.MentorshipRequestDecision(Inst, "m1", false, "Engineering", "r1"), "Mentorship Request Declined", "Your mentorship request in Engineering was declined.", "MentorshipRequestDecision", "/mentorship"),
            "forum" => (NotificationRequest.ForumReply(Inst, "m1", "Kofi", "Best first job?", "t1"), "New Reply to Your Thread", "Kofi replied to \"Best first job?\".", "ForumReply", "/forum/t1"),
            "referral" => (NotificationRequest.ReferralRegistered(Inst, "m1", "Yaw"), "Your Referral Joined!", "Yaw signed up using your referral link.", "ReferralRegistered", "/referrals"),
            "rsvp" => (NotificationRequest.EventRsvpConfirmed(Inst, "m1", "e1", "Gala"), "RSVP Confirmed", "You're confirmed for \"Gala\". See you there!", "EventRsvpConfirmed", "/events/e1"),
            "spotlight-approved" => (NotificationRequest.SpotlightDecision(Inst, "m1", true, null, "s1"), "Your Spotlight Was Approved!", "Congratulations — your spotlight submission is now live for everyone to see.", "SpotlightDecision", "/spotlights"),
            "spotlight-declined" => (NotificationRequest.SpotlightDecision(Inst, "m1", false, null, "s1"), "Spotlight Not Approved", "Your spotlight submission was not approved this time.", "SpotlightDecision", "/spotlights"),
            _ => (NotificationRequest.SpotlightDecision(Inst, "m1", false, "Photo unclear", "s1"), "Spotlight Not Approved", "Your spotlight submission was not approved. Reason: Photo unclear.", "SpotlightDecision", "/spotlights"),
        };

        await Send(rig, request);

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("m1", "Member", title, body, type), (n.RecipientId, n.RecipientType, n.Title, n.Body, n.Type));
        Assert.Equal($"https://umat.members.test{link}", n.ActionUrl);
    }

    // ── Broadcast ───────────────────────────────────────────────────────

    private static List<BroadcastRecipient> Audience() =>
        [new("m1", "a@x.com", "Ama", "0241"), new("m2", "b@x.com", "Kofi", null), new("m3", "", "Esi", "0551")];

    [WorkflowFact]
    public async Task A_broadcast_on_every_channel_reaches_each_recipient_on_each_channel_they_can_be_reached_by()
    {
        var rig = new Rig();

        await Send(rig, NotificationRequest.Broadcast(Inst, Audience(), "Campus closed", "Classes resume Monday", ["InApp", "Sms", "Email"], "https://img/x.png"));

        var notes = await rig.Notes();
        Assert.Equal(new[] { "m1", "m2", "m3" }, notes.Select(n => n.RecipientId));
        Assert.All(notes, n => Assert.Equal(("Campus closed", "Classes resume Monday", "Broadcast", "https://img/x.png"), (n.Title, n.Body, n.Type, n.ImageUrl)));

        Assert.Equal(new[] { "0241|UMaT: Classes resume Monday", "0551|UMaT: Classes resume Monday" }, rig.SmsSent.OrderBy(x => x));   // phone on file only

        Assert.Equal(new[] { "a@x.com", "b@x.com" }, rig.EmailsSent.Select(e => e.To.Single().Email).OrderBy(x => x));   // email on file only
        var email = rig.EmailsSent.First();
        Assert.Equal("notification", email.TemplateId);
    }

    [WorkflowFact]
    public async Task A_broadcast_overrides_individual_sms_opt_outs_by_design()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), Pref("m1", sms: false));
        await Send(rig, NotificationRequest.Broadcast(Inst, [new("m1", "a@x.com", "Ama", "0241")], null, "Emergency", ["Sms"]));
        Assert.Single(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task Broadcast_channels_are_case_insensitive_and_only_the_chosen_ones_are_used()
    {
        var rig = new Rig();
        await Send(rig, NotificationRequest.Broadcast(Inst, Audience(), null, "Hello", ["inapp"]));

        Assert.Equal(3, (await rig.Notes()).Count);
        Assert.Empty(rig.SmsSent);
        Assert.Empty(rig.EmailsSent);
        Assert.Equal("Announcement", (await rig.Notes()).First().Title);   // untitled broadcasts get a default
    }

    [WorkflowTheory]
    [InlineData("Sms")]
    [InlineData("Email")]
    public async Task A_broadcast_channel_the_institution_switched_off_is_skipped(string channel)
    {
        var rig = new Rig(i => { i.SmsNotificationsEnabled = channel != "Sms"; i.EmailNotificationsEnabled = channel != "Email"; });

        await Send(rig, NotificationRequest.Broadcast(Inst, Audience(), "T", "M", [channel]));

        Assert.Empty(rig.SmsSent);
        Assert.Empty(rig.EmailsSent);
    }

    [WorkflowFact]
    public async Task A_broadcast_with_no_recipients_does_nothing()
    {
        var rig = new Rig();
        await Send(rig, NotificationRequest.Broadcast(Inst, [], "T", "M", ["InApp", "Sms", "Email"]));
        Assert.Empty(await rig.Notes());
        Assert.Empty(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task Sms_text_is_made_safe_for_the_gsm_alphabet()
    {
        var rig = new Rig();
        await Send(rig, NotificationRequest.Broadcast(Inst, [new("m1", "", "A", "0241")], null, "Fees due — ₵200 ‘soon’ “please”…", ["Sms"]));
        Assert.Equal("0241|UMaT: Fees due - GHS 200 'soon' \"please\"...", rig.SmsSent.Single());
    }

    // ── Class notes ─────────────────────────────────────────────────────

    [WorkflowFact]
    public async Task A_class_note_alerts_classmates_but_not_its_author()
    {
        var rig = new Rig();
        await rig.Seed(Person("author", 2015), Person("mate", 2015), Person("other", 2016), Pref("mate", sms: true),
            new ClassNote { Id = "n1", AuthorId = "author", YearGroup = 2015 }, Device("mate"));

        await Send(rig, NotificationRequest.ClassNoteAlert(Inst, "n1", "Kojo"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("mate", "New Class Note", "Kojo posted a note to the Class of 2015 wall.", "https://umat.members.test/class-notes"), (n.RecipientId, n.Title, n.Body, n.ActionUrl));
        Assert.Contains("0241000000|UMaT: Kojo posted a note to the Class of 2015 wall. https://umat.members.test/class-notes", rig.SmsSent);
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), "New Class Note", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    [WorkflowFact]
    public async Task A_cancelled_event_notifies_attendees_in_app_and_by_sms_when_opted_in()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), Pref("m1", sms: true));

        await Send(rig, NotificationRequest.EventCancelled(Inst, "m1", "e1", "Gala"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("Event cancelled", "\"Gala\" has been cancelled.", "EventCancelled", "Event"), (n.Title, n.Body, n.Type, n.RelatedEntityType));
        Assert.Equal("https://umat.members.test/events/e1", n.ActionUrl);
        Assert.Single(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task A_member_who_opted_out_of_event_reminders_is_not_told_about_event_changes()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), Pref("m1", sms: true, eventReminders: false));

        await Send(rig, NotificationRequest.EventCancelled(Inst, "m1", "e1", "Gala"));
        await Send(rig, NotificationRequest.EventDetailsChanged(Inst, "m1", "e1", "Gala"));

        Assert.Empty(await rig.Notes());
        Assert.Empty(rig.SmsSent);
    }

    [WorkflowFact]
    public async Task Cancelling_your_own_rsvp_leaves_an_in_app_record_but_sends_no_sms_or_push()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), Pref("m1", sms: true), Device("m1"));

        await Send(rig, NotificationRequest.EventRsvpCancelled(Inst, "m1", "e1", "Gala"));

        Assert.Single(await rig.Notes());
        Assert.Empty(rig.SmsSent);
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [WorkflowFact]
    public async Task A_new_event_update_reaches_the_attendee_in_app_and_by_push_but_never_by_sms_or_email()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), Pref("m1", sms: true), Device("m1"));

        await Send(rig, NotificationRequest.EventUpdatePosted(Inst, "m1", "e1", "Gala", "Day 2 schedule", "Schedule"));

        var n = Assert.Single(await rig.Notes());
        Assert.Equal(("Schedule: Day 2 schedule", "New update for \"Gala\".", "EventUpdatePosted", "Event"), (n.Title, n.Body, n.Type, n.RelatedEntityType));
        Assert.Equal("https://umat.members.test/events/e1#updates", n.ActionUrl);   // opens the event at its updates
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), "Schedule: Day 2 schedule", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(rig.SmsSent);          // an event can have hundreds of attendees: no per-message cost
        Assert.Empty(rig.EmailsSent);
    }

    [WorkflowFact]
    public async Task A_member_who_opted_out_of_event_reminders_is_not_told_about_event_updates()
    {
        var rig = new Rig();
        await rig.Seed(Person("m1"), Pref("m1", eventReminders: false), Device("m1"));

        await Send(rig, NotificationRequest.EventUpdatePosted(Inst, "m1", "e1", "Gala", "Photos", "Photos & video"));

        Assert.Empty(await rig.Notes());
        rig.Push.Verify(p => p.SendAsync(It.IsAny<PushSubscriptionDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [WorkflowFact]
    public async Task An_event_update_for_an_unknown_member_does_nothing()
    {
        var rig = new Rig();
        await Send(rig, NotificationRequest.EventUpdatePosted(Inst, "ghost", "e1", "Gala", "T", "Recap"));
        Assert.Empty(await rig.Notes());
    }

    [WorkflowFact]
    public async Task A_lifecycle_notification_for_an_unknown_or_missing_member_does_nothing()
    {
        var rig = new Rig();
        await Send(rig, NotificationRequest.EventCancelled(Inst, "ghost", "e1", "Gala"));
        await Send(rig, NotificationRequest.MemberLifecycle(NotificationKind.EventCancelled, Inst, "", "T", "M"));
        Assert.Empty(await rig.Notes());
    }

    [WorkflowFact]
    public async Task A_birthday_shoutout_goes_to_everyone_except_the_celebrant()
    {
        var rig = new Rig();
        await rig.Seed(Person("celeb"), Person("a"), Person("b"));

        await Send(rig, NotificationRequest.BirthdayShoutout(Inst, "celeb", "Ama", "thread-1"));

        var notes = await rig.Notes();
        Assert.Equal(new[] { "a", "b" }, notes.Select(n => n.RecipientId));
        Assert.All(notes, n => Assert.Equal(("Birthday Shoutout", "It's Ama's birthday today! Drop by the forum and wish them well.", "https://umat.members.test/forum/thread-1"), (n.Title, n.Body, n.ActionUrl)));
    }

    [WorkflowFact]
    public async Task A_birthday_with_nobody_else_active_does_nothing()
    {
        var rig = new Rig();
        await rig.Seed(Person("celeb"));
        await Send(rig, NotificationRequest.BirthdayShoutout(Inst, "celeb", "Ama", "t"));
        Assert.Empty(await rig.Notes());
    }

    // ── Direct email ────────────────────────────────────────────────────

    [WorkflowFact]
    public async Task An_email_request_is_sent_through_the_email_service_as_is()
    {
        var rig = new Rig();
        var email = new SendEmailRequest { TemplateId = "reset-password", To = [new EmailContact { Email = "a@x.com", Name = "Ama" }] };

        await Send(rig, NotificationRequest.Email(email, "reset"));

        var sent = Assert.Single(rig.EmailsSent);
        Assert.Equal(("reset-password", "a@x.com"), (sent.TemplateId, sent.To.Single().Email));
    }

    [WorkflowFact]
    public async Task A_failed_email_send_does_not_fail_the_workflow()
    {
        var rig = new Rig();
        rig.Email.Setup(e => e.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailtrapResponse<MailtrapSendMessageResponse> { Success = false, Error = "rejected" });

        await Send(rig, NotificationRequest.Email(new SendEmailRequest { TemplateId = "x", To = [new EmailContact { Email = "a@x.com" }] }, "ctx"));
        // reaching here without an exception is the assertion
    }

    // ── Idempotency and push ────────────────────────────────────────────

    [WorkflowFact]
    public async Task Sending_the_same_alert_twice_does_not_duplicate_in_app_rows()
    {
        var rig = new Rig();
        await rig.Seed(Person("a"), new Job { Id = "j1", Title = "T", Company = "C", Location = "L" });

        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));
        await Send(rig, NotificationRequest.JobAlert(Inst, "j1"));

        Assert.Single(await rig.Notes());
    }

    [WorkflowFact]
    public async Task Staff_push_goes_to_the_staff_owner_type_not_the_member_one()
    {
        var rig = new Rig();
        await rig.Seed(new StaffEntity { Id = "s1", InstitutionId = Inst }, Device("s1", "InstitutionStaff"), Device("s1", "Member"));

        await Send(rig, NotificationRequest.NewMemberPendingApproval(Inst, "m9", "Kofi", "k@x.com"));

        rig.Push.Verify(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint.EndsWith("InstitutionStaff")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        rig.Push.Verify(p => p.SendAsync(It.Is<PushSubscriptionDto>(d => d.Endpoint.EndsWith("Member")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

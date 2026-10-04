using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Member.Api.Tests;

public class NotificationPreferenceServiceTests
{
    private const string Tenant = "inst-1";

    private static (NotificationPreferenceService service, string db) Create(string? db = null)
    {
        db ??= TestDb.NewName();
        var ctx = TestDb.Create(db, Tenant);
        return (new NotificationPreferenceService(new AlumniPgRepository<NotificationPreference>(ctx), NullLogger<NotificationPreferenceService>.Instance), db);
    }

    private static UpdateNotificationPreferenceRequest Request(string digest = "Weekly", bool sms = false) =>
        new(MembershipReminders: false, CampaignAlerts: false, EventReminders: true, JobAlerts: false, ClassNoteAlerts: true,
            SpotlightAlerts: false, SmsAlerts: sms, WhatsAppAlerts: sms, DigestFrequency: digest);

    [Fact]
    public async Task First_read_creates_defaults_with_everything_on_and_nothing_external()
    {
        var (service, db) = Create();

        var prefs = (await service.GetPreferencesAsync("m1")).Data!;

        Assert.True(prefs.MembershipReminders && prefs.CampaignAlerts && prefs.EventReminders && prefs.JobAlerts && prefs.ClassNoteAlerts && prefs.SpotlightAlerts);
        Assert.False(prefs.SmsAlerts);
        Assert.False(prefs.WhatsAppAlerts);
        var row = await TestDb.Create(db, Tenant).NotificationPreferences.SingleAsync();
        Assert.Equal(("m1", "m1"), (row.MemberId, row.CreatedBy));
    }

    [Fact]
    public async Task Reading_twice_does_not_create_a_second_row()
    {
        var (service, db) = Create();
        await service.GetPreferencesAsync("m1");
        var (again, _) = Create(db);
        await again.GetPreferencesAsync("m1");
        Assert.Equal(1, await TestDb.Create(db, Tenant).NotificationPreferences.CountAsync());
    }

    [Fact]
    public async Task Preferences_are_per_member()
    {
        var (service, db) = Create();
        await service.GetPreferencesAsync("m1");
        var (other, _) = Create(db);
        await other.GetPreferencesAsync("m2");
        Assert.Equal(2, await TestDb.Create(db, Tenant).NotificationPreferences.CountAsync());
    }

    [Fact]
    public async Task Updating_saves_every_switch_and_returns_them()
    {
        var (service, db) = Create();

        var response = await service.UpdatePreferencesAsync(Request("Monthly", sms: true), "m1");

        Assert.Equal(200, response.Code);
        var row = await TestDb.Create(db, Tenant).NotificationPreferences.SingleAsync();
        Assert.Equal((false, false, true, false, true, false, true, true, "Monthly", "m1"),
            (row.MembershipReminders, row.CampaignAlerts, row.EventReminders, row.JobAlerts, row.ClassNoteAlerts, row.SpotlightAlerts, row.SmsAlerts, row.WhatsAppAlerts, row.DigestFrequency, row.UpdatedBy));
        Assert.Equal("Monthly", response.Data!.DigestFrequency);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Weekly")]
    [InlineData("Monthly")]
    public async Task The_three_digest_frequencies_are_accepted(string digest)
    {
        var (service, _) = Create();
        Assert.Equal(200, (await service.UpdatePreferencesAsync(Request(digest), "m1")).Code);
    }

    [Theory]
    [InlineData("Daily")]
    [InlineData("weekly")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Any_other_digest_frequency_is_rejected_and_nothing_is_saved(string? digest)
    {
        var (service, db) = Create();

        var response = await service.UpdatePreferencesAsync(Request(digest!), "m1");

        Assert.Equal(400, response.Code);
        Assert.Equal(0, await TestDb.Create(db, Tenant).NotificationPreferences.CountAsync());
    }

    [Fact]
    public async Task Updating_an_existing_row_changes_it_in_place()
    {
        var (service, db) = Create();
        await service.GetPreferencesAsync("m1");
        var (second, _) = Create(db);

        await second.UpdatePreferencesAsync(Request("None"), "m1");

        var rows = await TestDb.Create(db, Tenant).NotificationPreferences.ToListAsync();
        Assert.Equal("None", Assert.Single(rows).DigestFrequency);
    }
}

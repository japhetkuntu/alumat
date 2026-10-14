using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Platform.Api.Tests;

[Collection("PlatformWhatsAppSwitch")]   // the answer is remembered process-wide, so these run one at a time
public class PlatformWhatsAppSwitchTests
{
    private static (PlatformSettingsService service, PlatformWhatsAppSwitch gate, string db, Mock<IAuditLogService> audit) Create(bool? botEnabled, bool configured = true)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            if (botEnabled is { } on) db.Add(new PlatformSettings { Id = PlatformSettings.SingletonId, WhatsAppBotEnabled = on });
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name);
        var audit = new Mock<IAuditLogService>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["WaSenderConfig:Enabled"] = configured ? "true" : "false" }).Build();
        var repo = new AlumniPgRepository<PlatformSettings>(ctx);
        PlatformWhatsAppSwitch.Forget();
        return (new PlatformSettingsService(repo, audit.Object, config, NullLogger<PlatformSettingsService>.Instance), new PlatformWhatsAppSwitch(repo), name, audit);
    }

    [Fact]
    public async Task The_bot_is_on_until_someone_switches_it_off_even_before_any_settings_row_exists()
    {
        var (_, gate, _, _) = Create(botEnabled: null);

        Assert.True(await gate.IsOnAsync());
    }

    [Fact]
    public async Task Switching_it_off_is_seen_at_once_audited_and_reported_with_whether_the_channel_is_configured()
    {
        var (service, gate, _, audit) = Create(botEnabled: true);
        Assert.True(await gate.IsOnAsync());

        var result = await service.UpdateAsync(new UpdatePlatformSettingsRequest(false, WhatsAppBotEnabled: false), "staff-1", "Ama Admin");

        Assert.False(result.Data!.WhatsAppBotEnabled);
        Assert.True(result.Data.WhatsAppConfigured);
        Assert.False(await gate.IsOnAsync());   // no waiting for the remembered answer to expire
        audit.Verify(a => a.LogAsync("staff-1", "Ama Admin", It.Is<string>(m => m.Contains("OFF") && m.Contains("SMS and email")), "Platform settings"), Times.Once);
    }

    [Fact]
    public async Task An_older_client_that_does_not_send_the_switch_leaves_it_alone()
    {
        var (service, gate, _, audit) = Create(botEnabled: false);

        var result = await service.UpdateAsync(new UpdatePlatformSettingsRequest(true), "staff-1", "Ama Admin");

        Assert.False(result.Data!.WhatsAppBotEnabled);
        Assert.True(result.Data.BlockOverdueCampaignPayments);
        Assert.False(await gate.IsOnAsync());
        audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(m => m.Contains("WhatsApp")), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Switching_it_back_on_is_audited_too()
    {
        var (service, gate, _, audit) = Create(botEnabled: false);

        await service.UpdateAsync(new UpdatePlatformSettingsRequest(false, WhatsAppBotEnabled: true), "staff-1", "Ama Admin");

        Assert.True(await gate.IsOnAsync());
        audit.Verify(a => a.LogAsync("staff-1", "Ama Admin", It.Is<string>(m => m.Contains("ON")), "Platform settings"), Times.Once);
    }

    // ── Per institution ──────────────────────────────────────────────────

    private static (WhatsAppHealthService service, string db, Mock<IAuditLogService> audit) CreateHealth(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        var audit = new Mock<IAuditLogService>();
        var service = new WhatsAppHealthService(Mock.Of<ReservEase.Alumni.Whatsapp.Sdk.Services.IWhatsAppProvider>(),
            Microsoft.Extensions.Options.Options.Create(new ReservEase.Alumni.Whatsapp.Sdk.Options.WaSenderConfig()),
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>(ctx),
            new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.NotificationPreference>(ctx),
            new ConfigurationBuilder().Build(), audit.Object, NullLogger<WhatsAppHealthService>.Instance);
        return (service, name, audit);
    }

    [Fact]
    public async Task Deactivating_one_institution_changes_only_that_one_and_is_audited_once()
    {
        var (service, db, audit) = CreateHealth(
            new Institution { Id = "i1", Slug = "alpha", Name = "Alpha", Status = "Active" },
            new Institution { Id = "i2", Slug = "bravo", Name = "Bravo", Status = "Active" });

        var off = await service.SetInstitutionAsync("i1", false, "staff-1", "Ama Admin");
        await service.SetInstitutionAsync("i1", false, "staff-1", "Ama Admin");   // no change, no second entry

        Assert.False(off.Data!.PlatformEnabled);
        using var ctx = TestDb.Create(db);
        Assert.False(ctx.Institutions.Single(i => i.Id == "i1").WhatsAppBotEnabled);
        Assert.True(ctx.Institutions.Single(i => i.Id == "i2").WhatsAppBotEnabled);
        audit.Verify(a => a.LogAsync("staff-1", "Ama Admin", It.Is<string>(m => m.Contains("deactivated") && m.Contains("SMS and email")), "Alpha"), Times.Once);

        var back = await service.SetInstitutionAsync("i1", true, "staff-1", "Ama Admin");
        Assert.True(back.Data!.PlatformEnabled);
        audit.Verify(a => a.LogAsync("staff-1", "Ama Admin", It.Is<string>(m => m.StartsWith("activated")), "Alpha"), Times.Once);
    }

    [Fact]
    public async Task The_list_shows_each_institutions_platform_switch_its_own_opt_out_and_adoption()
    {
        var (service, _, _) = CreateHealth(
            new Institution { Id = "i1", Slug = "alpha", Name = "Alpha", Status = "Active", WhatsAppBotEnabled = false },
            new Institution { Id = "i2", Slug = "bravo", Name = "Bravo", Status = "Active", DisabledFeatures = [InstitutionFeatures.WhatsApp] },
            new Institution { Id = "i3", Slug = "gone", Name = "Gone", Status = "Suspended" },
            new ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member { Id = "m1", InstitutionId = "i2", Status = "Active", PhoneVerifiedAt = DateTime.UtcNow, Email = "a@x.com", FirstName = "A", LastName = "B" });

        var rows = (await service.ListInstitutionsAsync()).Data!;

        Assert.Equal(["Alpha", "Bravo"], rows.Select(r => r.Name));
        Assert.Equal((false, false), (rows[0].PlatformEnabled, rows[0].InstitutionOptedOut));
        Assert.Equal((true, true, 1, 1), (rows[1].PlatformEnabled, rows[1].InstitutionOptedOut, rows[1].Connected, rows[1].ActiveMembers));
    }

    [Fact]
    public async Task An_unknown_institution_is_not_found()
    {
        var (service, _, _) = CreateHealth();

        var r = await service.SetInstitutionAsync("nope", false, "s", "n");

        Assert.Equal(404, r.Code);
    }

    [Fact]
    public void Whatsapp_runs_for_an_institution_only_when_neither_the_platform_nor_the_institution_has_it_off()
    {
        Assert.True(new Institution().IsWhatsAppAllowed());
        Assert.False(new Institution { WhatsAppBotEnabled = false }.IsWhatsAppAllowed());
        Assert.False(new Institution { DisabledFeatures = [InstitutionFeatures.WhatsApp] }.IsWhatsAppAllowed());
        Assert.False(new Institution { WhatsAppBotEnabled = false, DisabledFeatures = [InstitutionFeatures.WhatsApp] }.IsWhatsAppAllowed());
    }
}

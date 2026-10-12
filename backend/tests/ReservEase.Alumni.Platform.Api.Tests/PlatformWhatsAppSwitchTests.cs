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
}

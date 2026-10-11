using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using NotificationPreference = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.NotificationPreference;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Tests;

public class WhatsAppHealthTests
{
    private static WhatsAppHealthService Create(WaSenderConfig config, Mock<IWhatsAppProvider> provider, params object[] seed)
        => Create(config, provider, null, seed);

    private static WhatsAppHealthService Create(WaSenderConfig config, Mock<IWhatsAppProvider> provider, string? smsUnitCost, params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        return new WhatsAppHealthService(provider.Object, MsOptions.Create(config), new AlumniPgRepository<Institution>(ctx),
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<NotificationPreference>(ctx),
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["WhatsAppAssistant:SmsUnitCost"] = smsUnitCost }).Build(),
            NullLogger<WhatsAppHealthService>.Instance);
    }

    [Fact]
    public async Task Adoption_sums_alert_delivery_by_channel_and_estimates_the_saving_only_when_an_sms_cost_is_set()
    {
        var key = MessagingUsageKeys.For(DateTime.UtcNow);
        var seed = new object[]
        {
            new Institution { Id = "i1", Slug = "a", Name = "A", Status = "Active",
                MessagingUsage = new() { [key] = new MessagingUsageMonth { WhatsAppDelivered = 40, SmsSent = 10 } } },
            new Institution { Id = "i2", Slug = "b", Name = "B", Status = "Active",
                MessagingUsage = new() { [key] = new MessagingUsageMonth { WhatsAppDelivered = 60, SmsSent = 5 } } },
            new MemberEntity { Id = "m1", InstitutionId = "i1", Status = "Active", PhoneVerifiedAt = DateTime.UtcNow, Email = "a@x.com", FirstName = "A", LastName = "B" },
            new NotificationPreference { MemberId = "m1", InstitutionId = "i1", WhatsAppAlerts = true },
        };
        var provider = new Mock<IWhatsAppProvider>();
        var cfg = new WaSenderConfig { Enabled = true, ApiKey = "k" };

        var without = (await Create(cfg, provider, null, seed).GetAsync()).Data!.Adoption!;
        Assert.Equal((100, 15, 1, 1), (without.Months[^1].ByWhatsApp, without.Months[^1].BySms, without.AlertsOn, without.Institutions.Count(i => i.Connected > 0)));
        Assert.Null(without.EstimatedSmsSaved);

        var withCost = (await Create(cfg, provider, "0.05", seed).GetAsync()).Data!.Adoption!;
        Assert.Equal(5.00m, withCost.EstimatedSmsSaved);
        Assert.Equal("B", withCost.Institutions[0].Name);   // most WhatsApp deliveries first
    }

    [Fact]
    public async Task An_unconfigured_channel_is_reported_as_not_configured_and_the_provider_is_not_called()
    {
        var provider = new Mock<IWhatsAppProvider>();
        var s = Create(new WaSenderConfig(), provider);

        var d = (await s.GetAsync()).Data!;

        Assert.Equal((false, false, false, "not configured"), (d.ApiKeyConfigured, d.WebhookSecretConfigured, d.Connected, d.SessionState));
        provider.Verify(p => p.GetConnectionStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_configured_channel_reports_the_session_and_adoption_counts_without_any_secrets()
    {
        var provider = new Mock<IWhatsAppProvider>();
        provider.Setup(p => p.GetConnectionStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WhatsAppConnectionStatus(true, "connected"));
        var s = Create(new WaSenderConfig { Enabled = true, ApiKey = "k", WebhookSecret = "w", AssistantNumber = "233200000000" }, provider,
            new Institution { Id = "i1", Slug = "a", Name = "A", Status = "Active", WhatsAppGroups = [new WhatsAppGroupLink { Status = WhatsAppGroupStatuses.Active }, new WhatsAppGroupLink { Status = WhatsAppGroupStatuses.Detected }] },
            new Institution { Id = "i2", Slug = "b", Name = "B", Status = "Active", DisabledFeatures = [InstitutionFeatures.WhatsApp] },
            new MemberEntity { Id = "m1", InstitutionId = "i1", Status = "Active", PhoneVerifiedAt = DateTime.UtcNow, Email = "a@x.com", FirstName = "A", LastName = "B" },
            new MemberEntity { Id = "m2", InstitutionId = "i1", Status = "Active", Email = "b@x.com", FirstName = "A", LastName = "B" });

        var d = (await s.GetAsync()).Data!;

        Assert.Equal((true, true, "connected"), (d.Enabled, d.Connected, d.SessionState));
        Assert.Equal((1, 1, 1, 1), (d.InstitutionsWithGroups, d.ActiveGroups, d.VerifiedMembers, d.InstitutionsOptedOut));
        var json = System.Text.Json.JsonSerializer.Serialize(d);
        Assert.DoesNotContain("233200000000", json);
        Assert.DoesNotContain("\"k\"", json);
    }
}

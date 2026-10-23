using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Integrations;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>What platform staff can set for an institution's integration, and how its API keys are made and kept.</summary>
public class InstitutionIntegrationSetupTests : IDisposable
{
    private const string Inst = "inst-1";
    private readonly AlumniDbContext db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
    private readonly InstitutionIntegrationService service;

    public InstitutionIntegrationSetupTests()
    {
        (db, connection) = TestDb.CreateRelational();
        db.Add(new InstitutionEntity { Id = Inst, Name = "UMaT", Slug = "umat" });
        db.SaveChanges(); db.ChangeTracker.Clear();
        service = new InstitutionIntegrationService(
            new AlumniPgRepository<InstitutionIntegration>(db), new AlumniPgRepository<IntegrationApiKey>(db), new AlumniPgRepository<OnboardingLink>(db),
            new AlumniPgRepository<InstitutionEntity>(db), Mock.Of<IAuditLogService>(), NullLogger<InstitutionIntegrationService>.Instance);
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }

    private static UpdateIntegrationRequest Valid(bool enabled = true) =>
        new(enabled, " Off-boarding ", "https://offboard.example.edu/start", " Finish your off-boarding there ", 72, 48, 30);

    [Fact]
    public async Task Settings_are_saved_trimmed_and_read_back_with_nothing_set_before()
    {
        Assert.False((await service.GetAsync(Inst)).Data!.Enabled);

        var saved = (await service.UpdateAsync(Inst, Valid(), "u1", "Admin")).Data!;

        Assert.Equal((true, "Off-boarding", "Finish your off-boarding there", 72, 48, 30), (saved.Enabled, saved.PortalName, saved.BannerMessage, saved.BannerHours, saved.LinkExpiryHours, saved.StatusCacheDays));
        Assert.Equal("https://offboard.example.edu/start", (await service.GetAsync(Inst)).Data!.RedirectUrl);
    }

    [Theory]
    [InlineData("http://offboard.example.edu")]
    [InlineData("javascript:alert(1)")]
    [InlineData("offboard.example.edu")]
    public async Task The_redirect_must_be_a_full_https_address(string url)
    {
        Assert.Equal(400, (await service.UpdateAsync(Inst, Valid() with { RedirectUrl = url }, "u1", "Admin")).Code);
    }

    [Fact]
    public async Task It_cannot_be_turned_on_until_the_portal_url_and_message_are_set_but_can_be_saved_off()
    {
        var empty = new UpdateIntegrationRequest(true, "", "", "", 72, 48, 30);
        Assert.Equal(400, (await service.UpdateAsync(Inst, empty, "u1", "Admin")).Code);
        Assert.Equal(200, (await service.UpdateAsync(Inst, empty with { Enabled = false }, "u1", "Admin")).Code);
    }

    [Theory]
    [InlineData(0, 48, 30)]
    [InlineData(72, 0, 30)]
    [InlineData(72, 48, 0)]
    [InlineData(72, 48, 366)]
    [InlineData(721, 48, 30)]
    public async Task Durations_must_be_within_sensible_limits(int banner, int link, int cache)
    {
        Assert.Equal(400, (await service.UpdateAsync(Inst, Valid() with { BannerHours = banner, LinkExpiryHours = link, StatusCacheDays = cache }, "u1", "Admin")).Code);
    }

    [Fact]
    public async Task A_key_is_shown_once_stored_only_as_a_hash_and_can_authenticate_until_revoked()
    {
        await service.UpdateAsync(Inst, Valid(), "u1", "Admin");
        var created = (await service.CreateKeyAsync(Inst, new CreateIntegrationKeyRequest(" Their server "), "u1", "Admin")).Data!;

        Assert.StartsWith("alk_", created.ApiKey);
        Assert.Equal("Their server", created.Key.Name);
        var stored = db.IntegrationApiKeys.Single();
        Assert.Equal(IntegrationSecrets.Hash(created.ApiKey), stored.KeyHash);
        Assert.DoesNotContain(created.ApiKey, stored.KeyHash + stored.KeyPrefix + stored.Name);
        Assert.Equal(created.ApiKey[..10], created.Key.KeyPrefix);
        Assert.DoesNotContain(created.ApiKey, System.Text.Json.JsonSerializer.Serialize((await service.GetAsync(Inst)).Data));   // listing never returns it again

        var integrations = new IntegrationService(
            new AlumniPgRepository<IntegrationApiKey>(db), new AlumniPgRepository<InstitutionIntegration>(db), new AlumniPgRepository<InstitutionEntity>(db),
            new AlumniPgRepository<OnboardingLink>(db), new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>(db));
        Assert.Equal(Inst, (await integrations.AuthenticateAsync(created.ApiKey))!.InstitutionId);

        db.ChangeTracker.Clear();   // each request gets its own context in production
        Assert.Equal(200, (await service.RevokeKeyAsync(Inst, created.Key.Id, "u1", "Admin")).Code);
        Assert.Null(await integrations.AuthenticateAsync(created.ApiKey));
        Assert.Equal(404, (await service.RevokeKeyAsync("other-inst", created.Key.Id, "u1", "Admin")).Code);   // a key is only revocable under its own institution
    }

    [Fact]
    public async Task A_key_needs_a_name_and_two_keys_never_match()
    {
        Assert.Equal(400, (await service.CreateKeyAsync(Inst, new CreateIntegrationKeyRequest("  "), "u1", "Admin")).Code);
        var a = (await service.CreateKeyAsync(Inst, new CreateIntegrationKeyRequest("a"), "u1", "Admin")).Data!;
        var b = (await service.CreateKeyAsync(Inst, new CreateIntegrationKeyRequest("b"), "u1", "Admin")).Data!;
        Assert.NotEqual(a.ApiKey, b.ApiKey);
    }
}

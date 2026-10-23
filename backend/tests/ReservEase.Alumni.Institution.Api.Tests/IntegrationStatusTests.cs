using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Integrations;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>The partner status check: who may call it, what it answers, the cache in front of it, and the one-use links it hands out.</summary>
public class IntegrationStatusTests : IDisposable
{
    private const string Tenant = "inst-1";
    private const string GoodKey = "alk_theKeyThePartnerHolds1234567890abcdefghijklmnopq";

    private readonly AlumniDbContext db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
    private readonly FakeRedis redisStore = new();

    public IntegrationStatusTests()
    {
        (db, connection) = TestDb.CreateRelational(Tenant);
        db.Add(new InstitutionEntity { Id = Tenant, Name = "UMaT", Slug = "umat", Status = "Active" });
        db.Add(new InstitutionIntegration { InstitutionId = Tenant, Enabled = true, PortalName = "Off-boarding", RedirectUrl = "https://offboard.example.edu", BannerMessage = "Finish there", LinkExpiryHours = 48, StatusCacheDays = 30 });
        db.Add(new IntegrationApiKey { InstitutionId = Tenant, Name = "Their server", KeyPrefix = GoodKey[..10], KeyHash = IntegrationSecrets.Hash(GoodKey) });
        db.SaveChanges(); db.ChangeTracker.Clear();
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }

    private (IntegrationStatusService Service, IntegrationStatusCache Cache) Create()
    {
        var cache = new IntegrationStatusCache(redisStore);
        var integrations = new IntegrationService(
            new AlumniPgRepository<IntegrationApiKey>(db), new AlumniPgRepository<InstitutionIntegration>(db), new AlumniPgRepository<InstitutionEntity>(db),
            new AlumniPgRepository<OnboardingLink>(db), new AlumniPgRepository<MemberEntity>(db));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MemberPortalBaseDomain"] = "alumunion.test" }).Build();
        return (new IntegrationStatusService(integrations, cache, TestDb.Tenant(null), config, NullLogger<IntegrationStatusService>.Instance), cache);
    }

    private void AddMember(string studentId, string status = "Active", string institutionId = Tenant)
    {
        db.Add(new MemberEntity { Id = Guid.NewGuid().ToString("N"), InstitutionId = institutionId, FirstName = "Ama", LastName = "Mensah", Email = $"{Guid.NewGuid():N}@x.org", StudentId = studentId, Status = status });
        db.SaveChanges(); db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task A_missing_unknown_or_revoked_key_is_refused()
    {
        var (service, _) = Create();
        Assert.Equal(401, (await service.GetMemberStatusAsync(null, "S1")).Code);
        Assert.Equal(401, (await service.GetMemberStatusAsync("alk_nope", "S1")).Code);

        db.Update(db.IntegrationApiKeys.Single().Also(k => k.RevokedAt = DateTime.UtcNow));
        db.SaveChanges(); db.ChangeTracker.Clear();
        Assert.Equal(401, (await service.GetMemberStatusAsync(GoodKey, "S1")).Code);
    }

    [Fact]
    public async Task A_switched_off_integration_or_a_suspended_institution_is_refused()
    {
        var (service, _) = Create();
        db.Update(db.InstitutionIntegrations.Single().Also(s => s.Enabled = false));
        db.SaveChanges(); db.ChangeTracker.Clear();
        Assert.Equal(403, (await service.GetMemberStatusAsync(GoodKey, "S1")).Code);

        db.Update(db.InstitutionIntegrations.Single().Also(s => s.Enabled = true));
        db.Update(db.Institutions.Single().Also(i => i.Status = "Suspended"));
        db.SaveChanges(); db.ChangeTracker.Clear();
        Assert.Equal(403, (await service.GetMemberStatusAsync(GoodKey, "S1")).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_member_id_is_a_bad_request(string? id)
    {
        var (service, _) = Create();
        Assert.Equal(400, (await service.GetMemberStatusAsync(GoodKey, id)).Code);
        Assert.Equal(400, (await service.GetMemberStatusAsync(GoodKey, new string('9', 65))).Code);
    }

    [Theory]
    [InlineData("STU-001", "STU-001")]
    [InlineData("STU-001", "stu-001")]
    [InlineData("stu-001", "  STU-001 ")]
    public async Task An_existing_student_is_found_whatever_their_status_ignoring_case_and_spaces(string stored, string asked)
    {
        AddMember(stored, status: "Pending");
        var (service, _) = Create();

        var result = await service.GetMemberStatusAsync(GoodKey, asked);

        Assert.Equal(200, result.Code);
        Assert.True(result.Data!.Exists);
        Assert.Null(result.Data.RegistrationUrl);
    }

    [Fact]
    public async Task A_student_of_another_institution_does_not_count()
    {
        AddMember("STU-001", institutionId: "other-inst");
        var (service, _) = Create();

        var result = await service.GetMemberStatusAsync(GoodKey, "STU-001");

        Assert.False(result.Data!.Exists);
    }

    [Fact]
    public async Task An_unknown_student_gets_a_link_to_that_institutions_member_portal_that_expires_when_set()
    {
        var (service, _) = Create();
        var before = DateTime.UtcNow;

        var result = await service.GetMemberStatusAsync(GoodKey, "STU-404");

        Assert.Equal(200, result.Code);
        var data = result.Data!;
        Assert.False(data.Exists);
        Assert.StartsWith("https://umat.alumunion.test/register?ob=", data.RegistrationUrl);
        Assert.InRange(data.LinkExpiresAt!.Value, before.AddHours(48).AddSeconds(-5), DateTime.UtcNow.AddHours(48).AddSeconds(5));
        var link = db.OnboardingLinks.Single();
        Assert.Equal("STU-404", link.StudentId);
        Assert.DoesNotContain(link.TokenHash, data.RegistrationUrl!);   // only a hash is stored, never the token itself
    }

    [Fact]
    public async Task A_new_request_for_the_same_student_replaces_the_earlier_link()
    {
        var (service, _) = Create();
        await service.GetMemberStatusAsync(GoodKey, "STU-404");
        await service.GetMemberStatusAsync(GoodKey, "stu-404");

        var links = db.OnboardingLinks.OrderBy(l => l.CreatedAt).ToList();
        Assert.Equal(2, links.Count);
        Assert.NotNull(links[0].SupersededAt);
        Assert.Null(links[1].SupersededAt);

        await service.GetMemberStatusAsync(GoodKey, "STU-OTHER");
        Assert.Null(db.OnboardingLinks.Single(l => l.StudentId == "STU-OTHER").SupersededAt);   // another student's link is left alone
    }

    [Fact]
    public async Task A_student_who_exists_is_remembered_so_the_next_check_skips_the_database()
    {
        AddMember("STU-001");
        var (service, cache) = Create();
        await service.GetMemberStatusAsync(GoodKey, "STU-001");
        Assert.True(await cache.KnownToExistAsync(Tenant, "stu-001"));

        // Remove every member behind its back: the answer now comes from the cache alone.
        db.RemoveRange(db.Members.ToList());
        db.SaveChanges(); db.ChangeTracker.Clear();
        Assert.True((await service.GetMemberStatusAsync(GoodKey, "STU-001")).Data!.Exists);

        // Until it is forgotten, as when the member deletes their account.
        await cache.ForgetAsync(Tenant, "STU-001");
        Assert.False((await service.GetMemberStatusAsync(GoodKey, "STU-001")).Data!.Exists);
    }

    [Fact]
    public async Task A_student_who_does_not_exist_is_never_cached()
    {
        var (service, cache) = Create();
        await service.GetMemberStatusAsync(GoodKey, "STU-404");

        Assert.False(await cache.KnownToExistAsync(Tenant, "STU-404"));
        AddMember("STU-404");
        Assert.True((await service.GetMemberStatusAsync(GoodKey, "STU-404")).Data!.Exists);   // registering is seen straight away
    }

    [Fact]
    public async Task A_cache_that_is_down_never_breaks_the_check()
    {
        AddMember("STU-001");
        var down = new FakeRedis { Down = true };
        var integrations = new IntegrationService(
            new AlumniPgRepository<IntegrationApiKey>(db), new AlumniPgRepository<InstitutionIntegration>(db), new AlumniPgRepository<InstitutionEntity>(db),
            new AlumniPgRepository<OnboardingLink>(db), new AlumniPgRepository<MemberEntity>(db));
        var service = new IntegrationStatusService(integrations, new IntegrationStatusCache(down), TestDb.Tenant(null), new ConfigurationBuilder().Build(), NullLogger<IntegrationStatusService>.Instance);

        Assert.True((await service.GetMemberStatusAsync(GoodKey, "STU-001")).Data!.Exists);
    }
}

/// <summary>A Redis that keeps values in memory (as JSON, like the real one), or fails every call when told it is down.</summary>
internal sealed class FakeRedis : IRedisService<PublicContentCacheConfig>
{
    private readonly Dictionary<string, string> store = [];
    public bool Down { get; set; }

    private void Check() { if (Down) throw new InvalidOperationException("redis down"); }

    public Task<T?> GetAsync<T>(string key)
    {
        Check();
        return Task.FromResult(store.TryGetValue(key, out var json) ? System.Text.Json.JsonSerializer.Deserialize<T>(json) : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        Check();
        store[key] = System.Text.Json.JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key) { Check(); store.Remove(key); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string key) { Check(); return Task.FromResult(store.ContainsKey(key)); }
}

internal static class Tap
{
    public static T Also<T>(this T value, Action<T> change) { change(value); return value; }
}

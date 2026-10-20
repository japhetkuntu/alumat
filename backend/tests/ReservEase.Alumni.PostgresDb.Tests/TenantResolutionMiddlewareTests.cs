using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Middleware;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class TenantResolutionMiddlewareTests
{
    private sealed class Harness
    {
        public AlumniDbContext Db { get; }
        public CurrentTenantService Tenant { get; } = new();
        public InMemoryRedisService<TenantResolutionCacheConfig> Cache { get; } = new();
        public Dictionary<string, string?> Config { get; } = new() { ["PlatformBaseDomain"] = "alumunion.test" };
        public bool Environment_IsDevelopment { get; set; }
        public bool NextCalled { get; private set; }

        public Harness()
        {
            Db = TestDb.Create();
            Db.Institutions.AddRange(
                new Institution { Id = "umat", Slug = "umat", Name = "UMaT", CustomDomain = "alumni.umat.edu.gh", Status = "Active" },
                new Institution { Id = "knust", Slug = "knust", Name = "KNUST", Status = "Active" },
                new Institution { Id = "susp", Slug = "susp", Name = "Suspended Uni", Status = "Suspended" },
                new Institution { Id = "dflt", Slug = "default-school", Name = "Default", Status = "Active" });
            Db.SaveChanges();
        }

        public async Task<DefaultHttpContext> Send(string host, string path = "/api/x", IPAddress? remote = null, params (string, string)[] headers)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Host = new HostString(host);
            ctx.Request.Path = path;
            ctx.Connection.RemoteIpAddress = remote;
            ctx.Response.Body = new MemoryStream();
            foreach (var (k, v) in headers) ctx.Request.Headers[k] = v;

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.EnvironmentName).Returns(Environment_IsDevelopment ? Environments.Development : Environments.Production);

            var middleware = new TenantResolutionMiddleware(_ => { NextCalled = true; return Task.CompletedTask; });
            await middleware.InvokeAsync(ctx, Db, Tenant, new ConfigurationBuilder().AddInMemoryCollection(Config).Build(), env.Object, Cache);
            return ctx;
        }
    }

    private static string Body(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        return new StreamReader(ctx.Response.Body).ReadToEnd();
    }

    [Fact]
    public async Task Resolves_by_custom_domain()
    {
        var h = new Harness();
        var ctx = await h.Send("alumni.umat.edu.gh");

        Assert.Equal("umat", h.Tenant.InstitutionId);
        Assert.Equal("umat", h.Tenant.InstitutionSlug);
        Assert.Equal("umat", ((Institution)ctx.Items["Institution"]!).Id);
        Assert.True(h.NextCalled);
    }

    [Fact]
    public async Task Resolves_by_subdomain_of_the_platform_base_domain()
    {
        var h = new Harness();
        await h.Send("knust.alumunion.test");
        Assert.Equal("knust", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task Host_matching_is_case_insensitive()
    {
        var h = new Harness();
        await h.Send("KNUST.AlumUnion.TEST");
        Assert.Equal("knust", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task The_port_is_ignored()
    {
        var h = new Harness();
        await h.Send("knust.alumunion.test:8443");
        Assert.Equal("knust", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task A_custom_domain_wins_over_a_default_slug()
    {
        var h = new Harness();
        h.Config["DefaultInstitutionSlug"] = "default-school";
        await h.Send("alumni.umat.edu.gh");
        Assert.Equal("umat", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task An_unknown_host_falls_back_to_the_default_institution_slug()
    {
        var h = new Harness();
        h.Config["DefaultInstitutionSlug"] = "default-school";

        var ctx = await h.Send("localhost");

        Assert.Equal("dflt", h.Tenant.InstitutionId);
        Assert.NotNull(ctx.Items["Institution"]);
        Assert.True(h.NextCalled);
    }

    [Fact]
    public async Task An_unknown_host_without_a_default_leaves_the_tenant_unset_but_still_continues()
    {
        var h = new Harness();

        var ctx = await h.Send("nobody.alumunion.test");

        Assert.Null(h.Tenant.InstitutionId);
        Assert.False(ctx.Items.ContainsKey("Institution"));
        Assert.True(h.NextCalled);
    }

    [Fact]
    public async Task A_subdomain_of_some_other_domain_is_not_treated_as_a_slug()
    {
        var h = new Harness();
        await h.Send("knust.evil.example");
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task Without_a_base_domain_subdomains_do_not_resolve()
    {
        var h = new Harness();
        h.Config.Remove("PlatformBaseDomain");
        await h.Send("knust.alumunion.test");
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task A_suspended_institution_gets_403_json_and_the_pipeline_stops()
    {
        var h = new Harness();

        var ctx = await h.Send("susp.alumunion.test");

        Assert.Equal(403, ctx.Response.StatusCode);
        Assert.Equal("application/json", ctx.Response.ContentType?.Split(';')[0]);
        var json = JsonDocument.Parse(Body(ctx)).RootElement;
        Assert.Equal(403, json.GetProperty("code").GetInt32());
        Assert.Contains("suspended", json.GetProperty("message").GetString());
        Assert.False(h.NextCalled);
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Theory]
    [InlineData("/api/v1/callbacks/paystack")]
    [InlineData("/API/V1/CALLBACKS/PAYSTACK/verify")]
    public async Task The_paystack_callback_still_reaches_a_suspended_institution(string path)
    {
        var h = new Harness();

        var ctx = await h.Send("susp.alumunion.test", path);

        Assert.True(h.NextCalled);
        Assert.NotEqual(403, ctx.Response.StatusCode);
        Assert.Equal("susp", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task Successful_and_negative_lookups_are_cached_so_the_database_is_not_hit_again()
    {
        var h = new Harness();
        await h.Send("knust.alumunion.test");
        await h.Send("ghost.alumunion.test");

        Assert.Contains("tenant-resolution:host:knust.alumunion.test", h.Cache.Store.Keys);
        Assert.Contains("tenant-resolution:host:ghost.alumunion.test", h.Cache.Store.Keys);

        // Remove the row: a cached hit must still resolve.
        h.Db.Institutions.RemoveRange(h.Db.Institutions.Where(i => i.Id == "knust"));
        h.Db.SaveChanges();
        await h.Send("knust.alumunion.test");
        Assert.Equal("knust", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task A_cached_negative_result_is_not_confused_with_a_cache_miss()
    {
        var h = new Harness();
        await h.Send("ghost.alumunion.test");
        var writes = h.Cache.Writes.Count;

        await h.Send("ghost.alumunion.test");

        Assert.Equal(writes, h.Cache.Writes.Count);
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.5")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.7")]
    public async Task The_internal_tenant_host_header_is_honoured_from_private_and_loopback_addresses(string ip)
    {
        var h = new Harness();
        await h.Send("api-container", remote: IPAddress.Parse(ip), headers: ("X-Internal-Tenant-Host", "knust.alumunion.test"));
        Assert.Equal("knust", h.Tenant.InstitutionId);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("11.0.0.1")]
    [InlineData("2001:db8::1")]
    public async Task The_internal_tenant_host_header_is_ignored_from_public_addresses(string ip)
    {
        var h = new Harness();
        await h.Send("api-container", remote: IPAddress.Parse(ip), headers: ("X-Internal-Tenant-Host", "knust.alumunion.test"));
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task The_internal_tenant_host_header_is_ignored_when_the_remote_address_is_unknown()
    {
        var h = new Harness();
        await h.Send("api-container", remote: null, headers: ("X-Internal-Tenant-Host", "knust.alumunion.test"));
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task A_genuine_host_match_is_never_overridden_by_the_internal_header()
    {
        var h = new Harness();
        await h.Send("alumni.umat.edu.gh", remote: IPAddress.Loopback, headers: ("X-Internal-Tenant-Host", "knust.alumunion.test"));
        Assert.Equal("umat", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task A_blank_internal_header_is_ignored()
    {
        var h = new Harness();
        await h.Send("api-container", remote: IPAddress.Loopback, headers: ("X-Internal-Tenant-Host", "   "));
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task The_dev_slug_header_works_in_development()
    {
        var h = new Harness { Environment_IsDevelopment = true };
        await h.Send("localhost", headers: ("X-Institution-Slug", "  KNUST "));
        Assert.Equal("knust", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task The_dev_slug_header_is_ignored_outside_development()
    {
        var h = new Harness { Environment_IsDevelopment = false };
        await h.Send("localhost", headers: ("X-Institution-Slug", "knust"));
        Assert.Null(h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task The_dev_slug_header_never_overrides_a_real_host_match()
    {
        var h = new Harness { Environment_IsDevelopment = true };
        await h.Send("alumni.umat.edu.gh", headers: ("X-Institution-Slug", "knust"));
        Assert.Equal("umat", h.Tenant.InstitutionId);
    }

    [Fact]
    public async Task Extension_registers_the_middleware()
    {
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(Mock.Of<IServiceProvider>());
        Assert.Same(app, app.UseTenantResolution());
        await Task.CompletedTask;
    }

    [Fact]
    public void TenantCacheEntry_distinguishes_a_cached_nothing_from_no_entry()
    {
        var entry = new TenantCacheEntry(null);
        Assert.NotNull(entry);
        Assert.Null(entry.Institution);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(entry);
        Assert.NotNull(Newtonsoft.Json.JsonConvert.DeserializeObject<TenantCacheEntry>(json));
    }
}

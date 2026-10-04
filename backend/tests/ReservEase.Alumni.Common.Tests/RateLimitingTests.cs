using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservEase.Alumni.Common.Sdk.Extensions;

namespace ReservEase.Alumni.Common.Tests;

public class RateLimitingTests
{
    private static async Task<IHost> StartAsync()
    {
        var host = new HostBuilder().ConfigureWebHost(web => web
            .UseTestServer()
            .ConfigureServices(s => { s.AddRouting(); s.AddAlumniRateLimiting(); })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(e =>
                {
                    e.MapGet("/auth", () => "ok").RequireRateLimiting(RateLimitingExtensions.AuthPolicy);
                    e.MapGet("/public", () => "ok").RequireRateLimiting(RateLimitingExtensions.PublicReadPolicy);
                    e.MapGet("/open", () => "ok");
                });
            })).Build();
        await host.StartAsync();
        return host;
    }

    private static HttpRequestMessage Get(string path, string? forwardedFor = null, string host = "alumni.test")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        return request;
    }

    [Fact]
    public void Policy_names_are_stable()
    {
        Assert.Equal("auth", RateLimitingExtensions.AuthPolicy);
        Assert.Equal("public-read", RateLimitingExtensions.PublicReadPolicy);
    }

    [Fact]
    public async Task Auth_policy_allows_ten_requests_then_queues_five_then_rejects_with_429()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/auth", "1.1.1.1"))).StatusCode);

        // The next five wait in the queue for the window to reset, so they must not complete or be rejected.
        var queued = Enumerable.Range(0, 5).Select(_ => client.SendAsync(Get("/auth", "1.1.1.1"))).ToList();
        await Task.Delay(300);
        Assert.All(queued, t => Assert.False(t.IsCompleted));

        var rejected = await client.SendAsync(Get("/auth", "1.1.1.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task Different_callers_get_independent_buckets()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();
        for (var i = 0; i < 10; i++) await client.SendAsync(Get("/auth", "1.1.1.1"));
        var queued = Enumerable.Range(0, 5).Select(_ => client.SendAsync(Get("/auth", "1.1.1.1"))).ToList();
        await Task.Delay(200);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get("/auth", "1.1.1.1"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/auth", "2.2.2.2"))).StatusCode);
    }

    [Fact]
    public async Task The_same_ip_on_a_different_host_is_a_different_bucket()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();
        for (var i = 0; i < 10; i++) await client.SendAsync(Get("/auth", "1.1.1.1", "one.test"));
        var queued = Enumerable.Range(0, 5).Select(_ => client.SendAsync(Get("/auth", "1.1.1.1", "one.test"))).ToList();
        await Task.Delay(200);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get("/auth", "1.1.1.1", "one.test"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/auth", "1.1.1.1", "two.test"))).StatusCode);
    }

    [Fact]
    public async Task Only_the_first_forwarded_address_is_used_for_partitioning()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();
        for (var i = 0; i < 10; i++) await client.SendAsync(Get("/auth", "9.9.9.9, 10.0.0.1"));
        var queued = Enumerable.Range(0, 5).Select(_ => client.SendAsync(Get("/auth", "9.9.9.9, 10.0.0.2"))).ToList();
        await Task.Delay(200);

        // Same client IP (first entry) behind a different proxy hop still shares the bucket.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get("/auth", "9.9.9.9, 10.0.0.3"))).StatusCode);
    }

    [Fact]
    public async Task Without_a_forwarded_header_the_socket_address_is_used_and_requests_still_succeed()
    {
        using var host = await StartAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.GetTestClient().SendAsync(Get("/auth"))).StatusCode);
    }

    [Fact]
    public async Task Public_read_policy_is_generous()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();
        for (var i = 0; i < 400; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/public", "3.3.3.3"))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get("/public", "3.3.3.3"))).StatusCode);
    }

    [Fact]
    public async Task Endpoints_without_a_policy_are_never_limited()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();
        for (var i = 0; i < 50; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/open", "4.4.4.4"))).StatusCode);
    }
}

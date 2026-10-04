using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Sms.Sdk.Extensions;
using ReservEase.Alumni.Sms.Sdk.Options;
using ReservEase.Alumni.Sms.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Sms.Tests;

public class ArkeselSmsServiceTests
{
    private static ArkeselSmsService Create(FakeHttpHandler handler, ArkeselConfig? config = null) =>
        new(Options.Create(config ?? new ArkeselConfig { ApiKey = "key-123", SenderId = "UMaT", BaseUrl = "https://sms.test" }),
            new FakeHttpClientFactory(handler),
            NullLogger<ArkeselSmsService>.Instance);

    [Fact]
    public async Task Send_posts_to_v2_endpoint_with_sender_message_and_recipient()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);

        var ok = await Create(handler).SendSmsAsync("0241234567", "Hello there");

        Assert.True(ok);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://sms.test/api/v2/sms/send", request.RequestUri!.ToString());
        var body = handler.Bodies[0]!;
        Assert.Contains("\"sender\":\"UMaT\"", body);
        Assert.Contains("\"message\":\"Hello there\"", body);
        Assert.Contains("\"recipients\":[\"0241234567\"]", body);
    }

    [Fact]
    public async Task Send_sets_api_key_header_from_config()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);
        await Create(handler).SendSmsAsync("0241234567", "x");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("key-123", Assert.Single(request.Headers.GetValues("api-key")));
    }

    [Fact]
    public async Task Send_uses_the_named_Arkesel_client()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);
        var factory = new FakeHttpClientFactory(handler);
        var service = new ArkeselSmsService(Options.Create(new ArkeselConfig()), factory, NullLogger<ArkeselSmsService>.Instance);

        await service.SendSmsAsync("1", "m");

        Assert.Equal(new[] { "Arkesel" }, factory.RequestedNames);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Send_returns_false_on_non_success_status(HttpStatusCode status)
    {
        Assert.False(await Create(FakeHttpHandler.Json(status, "{\"message\":\"nope\"}")).SendSmsAsync("1", "m"));
    }

    [Fact]
    public async Task Send_swallows_exceptions_and_returns_false()
    {
        var handler = FakeHttpHandler.Throwing(new HttpRequestException("network down"));
        Assert.False(await Create(handler).SendSmsAsync("1", "m"));
    }

    [Fact]
    public void Config_defaults_point_at_arkesel()
    {
        var config = new ArkeselConfig();
        Assert.Equal("https://sms.arkesel.com", config.BaseUrl);
        Assert.Equal(string.Empty, config.ApiKey);
        Assert.Equal(string.Empty, config.SenderId);
    }

    [Fact]
    public void AddArkeselSmsService_binds_config_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArkeselConfig:ApiKey"] = "abc",
            ["ArkeselConfig:SenderId"] = "Alumni",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddArkeselSmsService(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var bound = scope.ServiceProvider.GetRequiredService<IOptions<ArkeselConfig>>().Value;
        Assert.Equal("abc", bound.ApiKey);
        Assert.Equal("Alumni", bound.SenderId);
        Assert.IsType<ArkeselSmsService>(scope.ServiceProvider.GetRequiredService<ISmsService>());
    }
}

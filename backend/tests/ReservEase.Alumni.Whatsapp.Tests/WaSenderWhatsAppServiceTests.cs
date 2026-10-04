using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Extensions;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;

namespace ReservEase.Alumni.Whatsapp.Tests;

public class WaSenderWhatsAppServiceTests
{
    private static WaSenderWhatsAppService Create(FakeHttpHandler handler, FakeHttpClientFactory? factory = null) =>
        new(Options.Create(new WaSenderConfig { ApiKey = "secret", BaseUrl = "https://wa.test" }),
            factory ?? new FakeHttpClientFactory(handler),
            NullLogger<WaSenderWhatsAppService>.Instance);

    [Fact]
    public async Task Send_posts_recipient_and_text_to_send_message_endpoint()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);

        Assert.True(await Create(handler).SendMessageAsync("233241234567", "Hi"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://wa.test/api/send-message", request.RequestUri!.ToString());
        Assert.Contains("\"to\":\"233241234567\"", handler.Bodies[0]);
        Assert.Contains("\"text\":\"Hi\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task Send_authenticates_with_bearer_token()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);
        await Create(handler).SendMessageAsync("1", "m");

        var auth = Assert.Single(handler.Requests).Headers.Authorization!;
        Assert.Equal("Bearer", auth.Scheme);
        Assert.Equal("secret", auth.Parameter);
    }

    [Fact]
    public async Task Send_uses_the_named_WaSender_client()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);
        var factory = new FakeHttpClientFactory(handler);
        await Create(handler, factory).SendMessageAsync("1", "m");
        Assert.Equal(new[] { "WaSender" }, factory.RequestedNames);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Send_returns_false_on_failure_status(HttpStatusCode status)
    {
        Assert.False(await Create(FakeHttpHandler.Json(status)).SendMessageAsync("1", "m"));
    }

    [Fact]
    public async Task Send_swallows_exceptions_and_returns_false()
    {
        Assert.False(await Create(FakeHttpHandler.Throwing(new TaskCanceledException())).SendMessageAsync("1", "m"));
    }

    [Fact]
    public void Config_defaults()
    {
        var config = new WaSenderConfig();
        Assert.Equal("https://www.wasenderapi.com", config.BaseUrl);
        Assert.Equal(string.Empty, config.ApiKey);
    }

    [Fact]
    public void AddWaSenderWhatsAppService_binds_config_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WaSenderConfig:ApiKey"] = "k",
            ["WaSenderConfig:BaseUrl"] = "https://other.test",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddWaSenderWhatsAppService(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var bound = scope.ServiceProvider.GetRequiredService<IOptions<WaSenderConfig>>().Value;
        Assert.Equal("k", bound.ApiKey);
        Assert.Equal("https://other.test", bound.BaseUrl);
        Assert.IsType<WaSenderWhatsAppService>(scope.ServiceProvider.GetRequiredService<IWhatsAppService>());
    }
}

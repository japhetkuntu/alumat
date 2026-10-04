using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.WebPush.Sdk.Extensions;
using ReservEase.Alumni.WebPush.Sdk.Options;
using ReservEase.Alumni.WebPush.Sdk.Services;

namespace ReservEase.Alumni.WebPush.Tests;

public class WebPushServiceTests
{
    [Fact]
    public async Task Send_with_invalid_vapid_keys_is_a_transient_failure_not_an_exception()
    {
        var service = new WebPushService(
            Options.Create(new WebPushConfig { VapidPublicKey = "not-a-key", VapidPrivateKey = "not-a-key" }),
            NullLogger<WebPushService>.Instance);

        var result = await service.SendAsync(new PushSubscriptionDto("https://push.example/abc", "p256dh", "auth"), "t", "b", null);

        Assert.Equal(WebPushSendResult.TransientFailure, result);
    }

    [Fact]
    public async Task Send_with_malformed_subscription_is_a_transient_failure()
    {
        var keys = global::WebPush.VapidHelper.GenerateVapidKeys();
        var service = new WebPushService(
            Options.Create(new WebPushConfig { VapidPublicKey = keys.PublicKey, VapidPrivateKey = keys.PrivateKey }),
            NullLogger<WebPushService>.Instance);

        var result = await service.SendAsync(new PushSubscriptionDto("not a url", "bad", "bad"), "t", "b", "/x");

        Assert.Equal(WebPushSendResult.TransientFailure, result);
    }

    [Fact]
    public void Config_defaults_have_a_vapid_subject()
    {
        var config = new WebPushConfig();
        Assert.StartsWith("mailto:", config.VapidSubject);
        Assert.Equal(string.Empty, config.VapidPublicKey);
        Assert.Equal(string.Empty, config.VapidPrivateKey);
    }

    [Fact]
    public void PushSubscriptionDto_has_value_equality()
    {
        Assert.Equal(new PushSubscriptionDto("e", "p", "a"), new PushSubscriptionDto("e", "p", "a"));
        Assert.NotEqual(new PushSubscriptionDto("e", "p", "a"), new PushSubscriptionDto("e2", "p", "a"));
    }

    [Fact]
    public void AddWebPushService_binds_config_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebPushConfig:VapidPublicKey"] = "pub",
            ["WebPushConfig:VapidSubject"] = "mailto:a@b.c",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddWebPushService(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var bound = scope.ServiceProvider.GetRequiredService<IOptions<WebPushConfig>>().Value;
        Assert.Equal("pub", bound.VapidPublicKey);
        Assert.Equal("mailto:a@b.c", bound.VapidSubject);
        Assert.IsType<WebPushService>(scope.ServiceProvider.GetRequiredService<IWebPushService>());
    }
}

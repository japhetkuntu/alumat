using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.WebPush.Sdk.Options;
using ReservEase.Alumni.WebPush.Sdk.Services;

namespace ReservEase.Alumni.WebPush.Sdk.Extensions;

public static class WebPushExtensionService
{
    public static IServiceCollection AddWebPushService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebPushConfig>(configuration.GetSection(nameof(WebPushConfig)));
        services.AddScoped<IWebPushService, WebPushService>();

        return services;
    }
}

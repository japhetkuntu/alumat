using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;

namespace ReservEase.Alumni.Whatsapp.Sdk.Extensions;

public static class WaSenderExtensionService
{
    public static IServiceCollection AddWaSenderWhatsAppService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WaSenderConfig>(configuration.GetSection(nameof(WaSenderConfig)));
        services.AddHttpClient("WaSender");
        services.AddScoped<IWhatsAppService, WaSenderWhatsAppService>();

        return services;
    }
}

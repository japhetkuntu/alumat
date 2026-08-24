using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Sms.Sdk.Options;
using ReservEase.Alumni.Sms.Sdk.Services;

namespace ReservEase.Alumni.Sms.Sdk.Extensions;

public static class ArkeselExtensionService
{
    public static IServiceCollection AddArkeselSmsService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArkeselConfig>(configuration.GetSection(nameof(ArkeselConfig)));
        services.AddHttpClient("Arkesel");
        services.AddScoped<ISmsService, ArkeselSmsService>();

        return services;
    }
}

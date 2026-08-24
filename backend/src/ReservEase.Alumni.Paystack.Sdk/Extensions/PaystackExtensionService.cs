using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;

namespace ReservEase.Alumni.Paystack.Sdk.Extensions;

public static class PaystackExtensionService
{
    public static IServiceCollection AddPaystackService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var config = new PaystackConfig();
        configuration.GetSection(nameof(PaystackConfig)).Bind(config);
        services.AddSingleton(config);
        services.AddHttpClient("Paystack");
        services.AddScoped<IPaystackService, PaystackService>();

        return services;
    }
}

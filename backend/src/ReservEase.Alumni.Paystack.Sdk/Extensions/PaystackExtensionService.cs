using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
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
        services.AddHttpClient("Paystack")
            .AddPolicyHandler(GetRetryPolicy());
        services.AddScoped<IPaystackService, PaystackService>();

        return services;
    }

    /// <summary>
    /// Retries only on transient failures (5xx, 408, and connection-level exceptions) —
    /// never on a 4xx, which means Paystack rejected the request itself and retrying it
    /// unchanged would just fail again. Exponential backoff (2s, 4s, 8s) gives a
    /// same-second blip on Paystack's side, or our own network, a real chance to clear
    /// before VerifyPaymentAsync gives up and marks the payment Failed.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
}

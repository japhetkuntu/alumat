using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Services;

namespace ReservEase.Alumni.Mailtrap.Sdk.Extensions;

public static class MailtrapExtensionService
{
    public static IServiceCollection AddMailtrapEmailService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MailtrapConfig>(configuration.GetSection(nameof(MailtrapConfig)));
        services.AddHttpClient("Mailtrap");
        services.AddScoped<IEmailService, MailtrapEmailService>();

        return services;
    }
}

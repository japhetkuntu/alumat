using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umat.Alumni.Mailhog.Sdk.Options;
using Umat.Alumni.Mailhog.Sdk.Services;
using Umat.Alumni.Mailtrap.Sdk.Services;

namespace Umat.Alumni.Mailhog.Sdk.Extensions;

public static class MailhogExtensionService
{
    public static IServiceCollection AddMailhogEmailService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var config = new MailhogConfig();
        configuration.GetSection(nameof(MailhogConfig)).Bind(config);
        services.AddSingleton(config);
        services.AddScoped<IEmailService, MailhogEmailService>();

        return services;
    }
}

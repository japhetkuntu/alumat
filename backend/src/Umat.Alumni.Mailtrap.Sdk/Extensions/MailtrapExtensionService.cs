using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umat.Alumni.Mailtrap.Sdk.Options;
using Umat.Alumni.Mailtrap.Sdk.Services;

namespace Umat.Alumni.Mailtrap.Sdk.Extensions;

public static class MailtrapExtensionService
{
    public static IServiceCollection AddMailtrapEmailService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var config = new MailtrapConfig();
        configuration.GetSection(nameof(MailtrapConfig)).Bind(config);
        services.AddSingleton(config);
        services.AddHttpClient("Mailtrap");
        services.AddScoped<IEmailService, MailtrapEmailService>();

        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Common.Sdk.Services;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

public static class GoogleAuthExtensions
{
    /// <summary>Binds the "GoogleAuthConfig" appsettings section and registers the shared
    /// Google ID-token verifier — one call from each API's Program.cs, mirroring AddBearerAuth.</summary>
    public static IServiceCollection AddGoogleAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleAuthConfig>(configuration.GetSection(nameof(GoogleAuthConfig)));
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        return services;
    }
}

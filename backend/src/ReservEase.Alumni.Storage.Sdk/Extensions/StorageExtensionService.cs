using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Storage.Sdk.Options;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Storage.Sdk.Extensions;

public static class StorageExtensionService
{
    public static IServiceCollection AddStorageService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageConfig>(configuration.GetSection(nameof(StorageConfig)));
        // A singleton so the one S3 client (and its pooled connections) is shared by every request instead of being rebuilt per call.
        services.AddSingleton<IStorageService, S3StorageService>();

        return services;
    }
}

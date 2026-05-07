using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umat.Alumni.Storage.Sdk.Options;
using Umat.Alumni.Storage.Sdk.Services;

namespace Umat.Alumni.Storage.Sdk.Extensions;

public static class StorageExtensionService
{
    public static IServiceCollection AddStorageService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageConfig>(configuration.GetSection(nameof(StorageConfig)));
        services.AddScoped<IStorageService, S3StorageService>();

        return services;
    }
}

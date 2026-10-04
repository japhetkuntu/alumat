using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Storage.Sdk.Extensions;
using ReservEase.Alumni.Storage.Sdk.Options;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Storage.Tests;

public class S3StorageServiceTests
{
    private static S3StorageService Create(Action<StorageConfig>? configure = null)
    {
        var config = new StorageConfig { BucketName = "bucket", CdnEndpoint = "https://cdn.test", FolderName = "alumni" };
        configure?.Invoke(config);
        return new S3StorageService(Options.Create(config));
    }

    [Fact]
    public void GetFileUrl_path_style_includes_bucket_and_default_folder()
        => Assert.Equal("https://cdn.test/bucket/alumni/a.png", Create().GetFileUrl("a.png"));

    [Fact]
    public void GetFileUrl_virtual_hosted_style_omits_bucket()
        => Assert.Equal("https://cdn.test/alumni/a.png", Create(c => c.CdnUrlIncludesBucket = false).GetFileUrl("a.png"));

    [Fact]
    public void GetFileUrl_explicit_folder_overrides_default()
        => Assert.Equal("https://cdn.test/bucket/logos/a.png", Create().GetFileUrl("a.png", "logos"));

    [Fact]
    public void GetFileUrl_slug_is_its_own_subfolder_beneath_folder()
        => Assert.Equal("https://cdn.test/bucket/logos/umat/a.png", Create().GetFileUrl("a.png", "logos", "umat"));

    [Fact]
    public void GetFileUrl_root_folder_prefixes_every_key()
        => Assert.Equal("https://cdn.test/bucket/shared/logos/umat/a.png", Create(c => c.RootFolder = "shared").GetFileUrl("a.png", "logos", "umat"));

    [Fact]
    public void GetFileUrl_skips_blank_segments_without_double_slashes()
    {
        var url = Create(c => { c.FolderName = ""; c.RootFolder = ""; }).GetFileUrl("a.png");
        Assert.Equal("https://cdn.test/bucket/a.png", url);
        Assert.DoesNotContain("//a", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetFileUrl_returns_empty_for_blank_file_name(string? name)
        => Assert.Equal(string.Empty, Create().GetFileUrl(name!));

    [Fact]
    public void StorageConfig_defaults()
    {
        var config = new StorageConfig();
        Assert.Equal("alumni", config.FolderName);
        Assert.True(config.CdnUrlIncludesBucket);
        Assert.Equal(string.Empty, config.RootFolder);
    }

    [Fact]
    public void AddStorageService_binds_config_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StorageConfig:BucketName"] = "b",
            ["StorageConfig:CdnUrlIncludesBucket"] = "false",
        }).Build();
        var services = new ServiceCollection();

        services.AddStorageService(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var bound = scope.ServiceProvider.GetRequiredService<IOptions<StorageConfig>>().Value;
        Assert.Equal("b", bound.BucketName);
        Assert.False(bound.CdnUrlIncludesBucket);
        Assert.IsType<S3StorageService>(scope.ServiceProvider.GetRequiredService<IStorageService>());
    }
}

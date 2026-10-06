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

public class PrivateFileLookupTests
{
    private sealed class FakeBucket(Action<StorageConfig>? configure, params string[] keys) : S3StorageService(Build(configure))
    {
        public List<string> Asked { get; } = new();

        private static IOptions<StorageConfig> Build(Action<StorageConfig>? configure)
        {
            var config = new StorageConfig { BucketName = "bucket" };
            configure?.Invoke(config);
            return Options.Create(config);
        }

        protected override Task<Stream> ReadObjectAsync(string fullKey)
        {
            Asked.Add(fullKey);
            if (keys.Contains(fullKey)) return Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fullKey)));
            throw new Amazon.S3.AmazonS3Exception("missing", Amazon.Runtime.ErrorType.Sender, "NoSuchKey", "req", System.Net.HttpStatusCode.NotFound);
        }
    }

    private static string Read(Stream s) => new StreamReader(s).ReadToEnd();

    [Fact]
    public async Task A_file_under_the_root_folder_is_found_there_first()
    {
        var bucket = new FakeBucket(c => c.RootFolder = "alumunion", "alumunion/reports/a.xlsx", "reports/a.xlsx");
        Assert.Equal("alumunion/reports/a.xlsx", Read(await bucket.OpenPrivateFileAsync("reports/a.xlsx")));
        Assert.Equal(new[] { "alumunion/reports/a.xlsx" }, bucket.Asked);
    }

    [Fact]
    public async Task A_file_written_without_the_root_folder_is_still_found_at_the_bucket_root()
    {
        var bucket = new FakeBucket(c => c.RootFolder = "alumunion", "reports/a.xlsx");
        Assert.Equal("reports/a.xlsx", Read(await bucket.OpenPrivateFileAsync("reports/a.xlsx")));
        Assert.Equal(new[] { "alumunion/reports/a.xlsx", "reports/a.xlsx" }, bucket.Asked);
    }

    [Fact]
    public async Task A_file_in_neither_place_is_reported_missing()
    {
        var bucket = new FakeBucket(c => c.RootFolder = "alumunion");
        var ex = await Assert.ThrowsAsync<Amazon.S3.AmazonS3Exception>(() => bucket.OpenPrivateFileAsync("reports/a.xlsx"));
        Assert.Equal("NoSuchKey", ex.ErrorCode);
        Assert.Equal(2, bucket.Asked.Count);
    }

    [Fact]
    public async Task Without_a_root_folder_there_is_only_one_place_to_look()
    {
        var bucket = new FakeBucket(null);
        await Assert.ThrowsAsync<Amazon.S3.AmazonS3Exception>(() => bucket.OpenPrivateFileAsync("reports/a.xlsx"));
        Assert.Equal(new[] { "reports/a.xlsx" }, bucket.Asked);
    }
}

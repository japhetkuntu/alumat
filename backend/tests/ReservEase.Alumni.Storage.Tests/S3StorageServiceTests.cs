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

    [Fact]
    public void The_storage_service_is_shared_by_every_request_so_the_s3_connection_is_reused()
    {
        var services = new ServiceCollection();
        services.AddStorageService(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        IStorageService First() { using var scope = provider.CreateScope(); return scope.ServiceProvider.GetRequiredService<IStorageService>(); }

        Assert.Same(First(), First());
    }
}

public class BulkUploadTests
{
    /// <summary>Stands in for the bucket: each upload takes a moment, and the most that ever ran together is recorded.</summary>
    private sealed class SlowBucket(int failOnIndex = -1) : S3StorageService(Options.Create(new StorageConfig { BucketName = "b" }))
    {
        private int running, peak, calls;
        public int Peak => peak;
        public int Calls => calls;

        public override async Task<string> UploadFileAsync(Microsoft.AspNetCore.Http.IFormFile file, string objectName, string folderName = "", string institutionSlug = "")
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            var call = Interlocked.Increment(ref calls) - 1;
            try
            {
                // Earlier files take longer, so a parallel run finishes out of order: the result order must not depend on that.
                await Task.Delay(Math.Max(5, 60 - int.Parse(file.FileName.Split('.')[0]) * 5));
                if (call == failOnIndex) throw new InvalidOperationException("bucket said no");
                return $"url-for-{file.FileName}";
            }
            finally { Interlocked.Decrement(ref running); }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current) { }
        }
    }

    private static List<Microsoft.AspNetCore.Http.IFormFile> Files(int count) =>
        Enumerable.Range(0, count)
            .Select(i => (Microsoft.AspNetCore.Http.IFormFile)new Microsoft.AspNetCore.Http.FormFile(new MemoryStream([1]), 0, 1, "file", $"{i}.png"))
            .ToList();

    [Fact]
    public async Task Urls_come_back_in_the_same_order_as_the_files_even_though_they_finish_out_of_order()
    {
        var urls = await new SlowBucket().BulkUploadFilesAsync(Files(10));

        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"url-for-{i}.png"), urls);
    }

    [Fact]
    public async Task Several_files_go_up_at_once_but_never_more_than_the_limit()
    {
        var bucket = new SlowBucket();

        await bucket.BulkUploadFilesAsync(Files(12));

        Assert.Equal(12, bucket.Calls);
        Assert.InRange(bucket.Peak, 2, S3StorageService.BulkUploadConcurrency);
    }

    [Fact]
    public async Task An_empty_batch_uploads_nothing()
        => Assert.Empty(await new SlowBucket().BulkUploadFilesAsync(new()));

    [Fact]
    public async Task One_failed_upload_fails_the_batch_instead_of_returning_a_half_empty_list()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => new SlowBucket(failOnIndex: 2).BulkUploadFilesAsync(Files(6)));
}

public class PrivateFileLookupTests
{
    private sealed class FakeBucket(Action<StorageConfig>? configure, params string[] keys) : S3StorageService(Build(configure))
    {
        public List<string> Asked { get; } = new();
        /// <summary>The bucket's top-level folders, as a listing would return them (with a trailing slash).</summary>
        public string[] Folders { get; init; } = [];
        public int Listings { get; private set; }

        protected override Task<IReadOnlyList<string>> ListTopLevelFoldersAsync()
        {
            Listings++;
            return Task.FromResult<IReadOnlyList<string>>(Folders);
        }

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
        Assert.Equal(1, bucket.Listings);
    }

    [Fact]
    public async Task A_file_the_worker_wrote_under_a_different_root_folder_is_still_found()
    {
        // The API has no root folder; the worker wrote under "alumunion".
        var bucket = new FakeBucket(null, "alumunion/reports/a.xlsx") { Folders = ["other/", "alumunion/"] };

        Assert.Equal("alumunion/reports/a.xlsx", Read(await bucket.OpenPrivateFileAsync("reports/a.xlsx")));
        Assert.Equal(new[] { "reports/a.xlsx", "other/reports/a.xlsx", "alumunion/reports/a.xlsx" }, bucket.Asked);
    }

    [Fact]
    public async Task A_folder_already_tried_is_not_asked_for_again()
    {
        var bucket = new FakeBucket(c => c.RootFolder = "alumunion") { Folders = ["alumunion/", "later/"] };

        await Assert.ThrowsAsync<Amazon.S3.AmazonS3Exception>(() => bucket.OpenPrivateFileAsync("reports/a.xlsx"));

        Assert.Equal(new[] { "alumunion/reports/a.xlsx", "reports/a.xlsx", "later/reports/a.xlsx" }, bucket.Asked);
    }

    [Fact]
    public async Task The_bucket_is_only_listed_when_the_obvious_places_miss()
    {
        var bucket = new FakeBucket(c => c.RootFolder = "alumunion", "alumunion/reports/a.xlsx");

        await bucket.OpenPrivateFileAsync("reports/a.xlsx");

        Assert.Equal(0, bucket.Listings);
    }

    [Fact]
    public async Task Without_a_root_folder_there_is_only_one_place_to_look()
    {
        var bucket = new FakeBucket(null);
        await Assert.ThrowsAsync<Amazon.S3.AmazonS3Exception>(() => bucket.OpenPrivateFileAsync("reports/a.xlsx"));
        Assert.Equal(new[] { "reports/a.xlsx" }, bucket.Asked); // and an empty folder list means nothing else to try
    }
}

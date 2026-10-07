using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Moq;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Reports.Sdk.Models;
using ReservEase.Alumni.Reports.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Reports.Tests;

public class ReportDownloadTests
{
    private const string Tenant = "inst-1";
    private static readonly ReportRequester Me = new(ReportAudiences.Institution, Tenant, "staff-1", "Kojo", "k@x.com", "SuperAdmin", false, [], [], []);

    private static async Task<(ReportJobService service, Mock<IStorageService> storage, CapturingLogger<ReportJobService> log)> Create(Action<ReportJob>? tweak = null)
    {
        var db = TestDb.NewName();
        var job = new ReportJob
        {
            Id = "j1", Audience = ReportAudiences.Institution, InstitutionId = Tenant, RequestedById = "staff-1", Status = ReportJobStatuses.Ready,
            FileKey = "reports/inst-1/j1/members.xlsx", FileName = "members.xlsx", Format = ReportFormats.Xlsx, ExpiresAt = DateTime.UtcNow.AddDays(3),
        };
        tweak?.Invoke(job);
        using (var seed = TestDb.Create(db, Tenant)) { seed.Add(job); await seed.SaveChangesAsync(); }

        var storage = new Mock<IStorageService>();
        var log = new CapturingLogger<ReportJobService>();
        var ctx = TestDb.Create(db, Tenant);
        var temporal = Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false);
        return (new ReportJobService(new AlumniPgRepository<ReportJob>(ctx), temporal, storage.Object, log), storage, log);
    }

    [Fact]
    public async Task A_ready_report_is_opened_from_storage_with_its_file_name_and_content_type()
    {
        var (service, storage, _) = await Create();
        storage.Setup(s => s.OpenPrivateFileAsync("reports/inst-1/j1/members.xlsx")).ReturnsAsync(new MemoryStream([1, 2, 3]));

        var download = await service.OpenDownloadAsync(Me, "j1");

        Assert.NotNull(download);
        Assert.Equal("members.xlsx", download!.FileName);
        Assert.Equal(ReportContentTypes.For(ReportFormats.Xlsx), download.ContentType);
    }

    [Theory]
    [InlineData("NoSuchKey")]
    [InlineData("NoSuchBucket")]
    public async Task A_file_missing_from_storage_is_reported_as_missing_and_logged_not_a_server_error(string code)
    {
        var (service, storage, log) = await Create();
        storage.Setup(s => s.OpenPrivateFileAsync(It.IsAny<string>())).ThrowsAsync(new AmazonS3Exception("gone", ErrorType.Sender, code, "req", HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<ReportFileMissingException>(() => service.OpenDownloadAsync(Me, "j1"));
        Assert.Contains(log.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning && e.Message.Contains("reports/inst-1/j1/members.xlsx"));
    }

    [Fact]
    public async Task Any_other_storage_failure_is_logged_and_reported_as_the_file_being_unavailable()
    {
        var (service, storage, log) = await Create();
        storage.Setup(s => s.OpenPrivateFileAsync(It.IsAny<string>())).ThrowsAsync(new AmazonS3Exception("denied", ErrorType.Sender, "AccessDenied", "req", HttpStatusCode.Forbidden));

        var ex = await Assert.ThrowsAsync<ReportFileUnavailableException>(() => service.OpenDownloadAsync(Me, "j1"));

        Assert.IsType<AmazonS3Exception>(ex.InnerException);
        Assert.Contains(log.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Exception is AmazonS3Exception);
    }

    [Theory]
    [InlineData("other-staff")]
    public async Task Another_person_cannot_download_it(string otherId)
    {
        var (service, storage, _) = await Create();
        var other = Me with { Id = otherId };
        Assert.Null(await service.OpenDownloadAsync(other, "j1"));
        storage.Verify(s => s.OpenPrivateFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_report_from_another_institution_or_audience_is_not_downloadable()
    {
        var (service, storage, _) = await Create();
        Assert.Null(await service.OpenDownloadAsync(Me with { InstitutionId = "other-inst" }, "j1"));
        Assert.Null(await service.OpenDownloadAsync(Me with { Audience = ReportAudiences.Platform }, "j1"));
        storage.Verify(s => s.OpenPrivateFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(ReportJobStatuses.Queued)]
    [InlineData(ReportJobStatuses.Running)]
    [InlineData(ReportJobStatuses.Failed)]
    [InlineData(ReportJobStatuses.Expired)]
    public async Task Only_a_ready_report_can_be_downloaded(string status)
    {
        var (service, storage, _) = await Create(j => j.Status = status);
        Assert.Null(await service.OpenDownloadAsync(Me, "j1"));
        storage.Verify(s => s.OpenPrivateFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_ready_report_past_an_old_expiry_date_is_still_downloadable_because_reports_never_expire()
    {
        var (service, storage, _) = await Create(j => j.ExpiresAt = DateTime.UtcNow.AddDays(-30));
        storage.Setup(s => s.OpenPrivateFileAsync(It.IsAny<string>())).ReturnsAsync(new MemoryStream([1]));
        Assert.NotNull(await service.OpenDownloadAsync(Me, "j1"));
    }

    [Fact]
    public async Task A_report_without_a_file_key_is_not_downloadable()
    {
        var (noKey, _, _) = await Create(j => j.FileKey = null);
        Assert.Null(await noKey.OpenDownloadAsync(Me, "j1"));
    }

    [Fact]
    public async Task A_report_with_no_expiry_set_can_still_be_downloaded()
    {
        var (service, storage, _) = await Create(j => j.ExpiresAt = null);
        storage.Setup(s => s.OpenPrivateFileAsync(It.IsAny<string>())).ReturnsAsync(new MemoryStream([1]));
        Assert.NotNull(await service.OpenDownloadAsync(Me, "j1"));
    }
}

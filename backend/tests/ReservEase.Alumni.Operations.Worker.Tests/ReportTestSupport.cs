using Microsoft.AspNetCore.Http;
using ReservEase.Alumni.Operations.Worker.Workflows.Reports;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>Private storage held in memory. The public-upload half of the interface is not something reports may use, so it throws.</summary>
public sealed class InMemoryPrivateStorage : IStorageService
{
    public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = [];
    public bool FailUploads { get; set; }

    public async Task UploadPrivateFileAsync(Stream fileStream, string key, string contentType)
    {
        if (FailUploads) throw new IOException("storage is down");
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer);
        Files[key] = (buffer.ToArray(), contentType);
    }

    public Task<Stream> OpenPrivateFileAsync(string key) => Task.FromResult<Stream>(new MemoryStream(Files[key].Content));
    public Task DeletePrivateFileAsync(string key) { Files.Remove(key); return Task.CompletedTask; }

    public Task<string> UploadFileAsync(IFormFile file, string objectName, string folderName = "", string institutionSlug = "") => throw new NotSupportedException("Reports must never be uploaded publicly.");
    public string GetFileUrl(string fileName, string folderName = "", string institutionSlug = "") => throw new NotSupportedException("A report has no URL.");
    public Task<List<string>> BulkUploadFilesAsync(List<IFormFile> files, string folderName = "", string institutionSlug = "") => throw new NotSupportedException();
    public Task<string> UploadFileAsync(Stream fileStream, string objectName, string folderName = "", string institutionSlug = "", string contentType = "application/pdf") => throw new NotSupportedException("Reports must never be uploaded publicly.");
}

public static class ReportRig
{
    public static ReportDataBuilder Builder(AlumniDbContext ctx) => new(
        new AlumniPgRepository<Member>(ctx), new AlumniPgRepository<Department>(ctx), new AlumniPgRepository<CommunityMembership>(ctx),
        new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<AlumniEvent>(ctx),
        new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
        new AlumniPgRepository<Institution>(ctx));

    public static async Task<List<object?[]>> Rows(ReportData data)
    {
        var rows = new List<object?[]>();
        await foreach (var row in data.Rows) rows.Add(row);
        return rows;
    }

    /// <summary>The values of one column, by its header — tests read reports the way a person does, not by column position.</summary>
    public static List<object?> Column(ReportData data, List<object?[]> rows, string header)
    {
        var index = data.Columns.ToList().FindIndex(c => c.Header == header);
        Assert.True(index >= 0, $"No column '{header}'. Columns: {string.Join(", ", data.Columns.Select(c => c.Header))}");
        return rows.Select(r => r[index]).ToList();
    }
}

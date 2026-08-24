using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Storage.Sdk.Services;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class UploadServiceTests
{
    [Fact]
    public async Task UploadImageAsync_ReturnsBadRequest_WhenFileIsNull()
    {
        var storageMock = new Mock<IStorageService>();
        var service = new UploadService(storageMock.Object, new NullLogger<UploadService>());

        var response = await service.UploadImageAsync(null!);

        Assert.Equal(400, response.Code);
        Assert.Equal("No file provided", response.Message);
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsBadRequest_WhenFileTooLarge()
    {
        var storageMock = new Mock<IStorageService>();
        var service = new UploadService(storageMock.Object, new NullLogger<UploadService>());

        var longBytes = new byte[21 * 1024 * 1024];
        using var stream = new MemoryStream(longBytes);
        var file = new FormFile(stream, 0, longBytes.Length, "id", "large.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };

        var response = await service.UploadFileAsync(file);

        Assert.Equal(400, response.Code);
        Assert.Equal("File exceeds 20MB limit", response.Message);
    }
}

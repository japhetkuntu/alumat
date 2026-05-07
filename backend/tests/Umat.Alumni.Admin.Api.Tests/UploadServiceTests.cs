using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umat.Alumni.Admin.Api.Services.Implementations;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Storage.Sdk.Services;
using Xunit;

namespace Umat.Alumni.Admin.Api.Tests;

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

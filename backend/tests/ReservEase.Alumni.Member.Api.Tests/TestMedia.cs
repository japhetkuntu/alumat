using Microsoft.AspNetCore.Http;
using Moq;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>A stand-in for storage that records what was uploaded, and files that look like (or only pretend to be) pictures.</summary>
internal sealed class FakeStorage
{
    public List<string> Uploaded { get; } = [];
    public IStorageService Service { get; }

    public FakeStorage()
    {
        var mock = new Mock<IStorageService>();
        mock.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IFormFile _, string name, string _, string _) => { var url = $"https://cdn.test/{name}"; Uploaded.Add(url); return url; });
        Service = mock.Object;
    }

    public GroupMedia Media(ICurrentTenantService tenant) => new(Service, tenant);
}

internal static class Files
{
    private static IFormFile Make(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "f", name);

    public static IFormFile Png(string name = "a.png") => Make([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0], name);
    public static IFormFile Jpeg(string name = "a.jpg") => Make([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0], name);
    public static IFormFile Gif(string name = "a.gif") => Make([0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0], name);
    public static IFormFile Webp(string name = "a.webp") => Make([0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50], name);
    /// <summary>A script wearing a picture's name.</summary>
    public static IFormFile Disguised(string name = "a.png") => Make("<script>alert(1)</script>"u8.ToArray(), name);
    public static IFormFile WithName(IFormFile real, string name) => Make(Bytes(real), name);
    public static IFormFile Empty(string name = "a.png") => Make([], name);
    public static IFormFile Big(string name = "big.png") => new FormFile(new BigStream(GroupMedia.MaxImageBytes + 1), 0, GroupMedia.MaxImageBytes + 1, "f", name);

    private static byte[] Bytes(IFormFile f) { using var m = new MemoryStream(); f.CopyTo(m); return m.ToArray(); }

    private sealed class BigStream(long length) : Stream
    {
        private long _pos;
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => _pos; set => _pos = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { var n = (int)Math.Min(count, length - _pos); for (var i = 0; i < n; i++) buffer[offset + i] = i == 0 && _pos == 0 ? (byte)0x89 : (byte)0; _pos += n; return n; }
        public override long Seek(long offset, SeekOrigin origin) => _pos = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _pos + offset, _ => length + offset };
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}

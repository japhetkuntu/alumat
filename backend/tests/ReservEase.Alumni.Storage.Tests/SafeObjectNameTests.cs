using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Storage.Tests;

/// <summary>Uploaded file names become part of a web address, so they must never contain anything an address or a CDN trips on.</summary>
public class SafeObjectNameTests
{
    [Theory]
    [InlineData("f702dd68-303b-4b8a-b621-dafc7b366fae_Mentora Academy C# Programming Course Flyer.png", "f702dd68-303b-4b8a-b621-dafc7b366fae_Mentora-Academy-C-Programming-Course-Flyer.png")]
    [InlineData("Photo #2 (final) 100%.PNG", "Photo-2-final-100.png")]
    [InlineData("a+b&c?d.jpg", "a-b-c-d.jpg")]
    [InlineData("résumé.pdf", "resume.pdf")]
    [InlineData("abc123.webp", "abc123.webp")]
    [InlineData("###.png", "file.png")]
    [InlineData("noextension", "noextension")]
    public void Names_are_made_safe_for_a_web_address(string given, string expected)
        => Assert.Equal(expected, S3StorageService.SafeObjectName(given));

    [Fact]
    public void A_very_long_name_is_shortened_but_keeps_its_extension()
    {
        var safe = S3StorageService.SafeObjectName(new string('a', 300) + ".jpg");
        Assert.EndsWith(".jpg", safe);
        Assert.True(safe.Length < 100);
    }
}

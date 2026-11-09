using ReservEase.Alumni.Member.Api.Services.Implementations;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>What a leader may attach: real pictures of a sensible size, web links, YouTube videos, and nothing else.</summary>
public class GroupMediaTests
{
    [Fact]
    public async Task Real_pictures_of_every_supported_kind_are_accepted()
    {
        foreach (var f in new[] { Files.Png(), Files.Jpeg(), Files.Jpeg("a.jpeg"), Files.Gif(), Files.Webp(), Files.Png("PHOTO.PNG") })
            Assert.Null(await GroupMedia.CheckImageAsync(f));
    }

    [Fact]
    public async Task A_file_is_judged_by_what_it_is_not_what_it_is_called()
    {
        Assert.NotNull(await GroupMedia.CheckImageAsync(Files.Disguised("holiday.png")));            // a script wearing a picture's name
        Assert.NotNull(await GroupMedia.CheckImageAsync(Files.WithName(Files.Png(), "a.svg")));      // a real picture, but a kind we don't take
        Assert.NotNull(await GroupMedia.CheckImageAsync(Files.WithName(Files.Png(), "a.html")));
        Assert.NotNull(await GroupMedia.CheckImageAsync(Files.WithName(Files.Png(), "noextension")));
        Assert.NotNull(await GroupMedia.CheckImageAsync(Files.WithName(Files.Jpeg(), "a.exe")));
        Assert.NotNull(await GroupMedia.CheckImageAsync(Files.WithName(Files.Jpeg(), "a.png.php")));
    }

    [Fact]
    public async Task An_empty_or_oversized_picture_is_refused()
    {
        Assert.Contains("empty", await GroupMedia.CheckImageAsync(Files.Empty()));
        Assert.Contains("5 MB", await GroupMedia.CheckImageAsync(Files.Big()));
    }

    [Fact]
    public async Task A_gallery_is_limited_counting_what_an_edited_post_already_keeps()
    {
        var eight = Enumerable.Range(0, GroupMedia.MaxGallery).Select(i => Files.Png($"{i}.png")).ToList();

        Assert.Null(await GroupMedia.CheckAsync(null, eight));
        Assert.Contains("up to 8", await GroupMedia.CheckAsync(null, [.. eight, Files.Png("9.png")]));
        Assert.Null(await GroupMedia.CheckAsync(null, [Files.Png(), Files.Png("b.png")], keptInGallery: 6));
        Assert.NotNull(await GroupMedia.CheckAsync(null, [Files.Png(), Files.Png("b.png"), Files.Png("c.png")], keptInGallery: 6));
    }

    [Fact]
    public async Task One_bad_file_in_a_set_refuses_the_whole_set()
    {
        Assert.NotNull(await GroupMedia.CheckAsync(Files.Png(), [Files.Png("ok.png"), Files.Disguised("bad.png")]));
        Assert.NotNull(await GroupMedia.CheckAsync(Files.Disguised(), [Files.Png()]));
        Assert.Null(await GroupMedia.CheckAsync(null, null));
    }

    [Fact]
    public void An_edited_post_keeps_only_pictures_it_already_had()
    {
        var had = new[] { "https://cdn.test/a.png", "https://cdn.test/b.png" };

        var kept = GroupMedia.Keep(had, ["https://cdn.test/a.png", "https://evil.example/tracker.png", "https://cdn.test/a.png"], ["https://cdn.test/new.png"]);

        Assert.Equal(["https://cdn.test/a.png", "https://cdn.test/new.png"], kept);   // b was dropped, the stranger ignored, the repeat collapsed
        Assert.Empty(GroupMedia.Keep(null, ["https://cdn.test/a.png"], []));
        Assert.Empty(GroupMedia.Keep(had, null, []));
    }

    [Theory]
    [InlineData("https://maps.google.com/?q=hall", true)]
    [InlineData("http://example.com/meet", true)]
    [InlineData("  https://zoom.us/j/123  ", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("ftp://files.example.com/x", false)]
    [InlineData("example.com/no-scheme", false)]
    [InlineData("not a link", false)]
    public void A_web_link_must_be_http_or_https(string? url, bool fine)
    {
        var problem = GroupMedia.CleanWebLink(url, out var clean);

        Assert.Equal(fine, problem is null);
        if (fine && !string.IsNullOrWhiteSpace(url)) Assert.Equal(url!.Trim(), clean);
        if (!fine) Assert.Null(clean);
    }

    [Fact]
    public void A_web_link_cannot_be_absurdly_long()
        => Assert.NotNull(GroupMedia.CleanWebLink("https://example.com/" + new string('a', 600), out _));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", true)]
    [InlineData("https://youtube.com/watch?v=abc", true)]
    [InlineData("https://m.youtube.com/watch?v=abc", true)]
    [InlineData("https://youtu.be/abc", true)]
    [InlineData("http://www.youtube.com/watch?v=abc", false)]       // not secure
    [InlineData("https://vimeo.com/123", false)]
    [InlineData("https://youtube.com.evil.example/watch", false)]    // looks like it, is not
    [InlineData("https://evil.example/youtube.com", false)]
    [InlineData("javascript:alert(1)", false)]
    public void Only_secure_youtube_links_are_videos(string url, bool fine)
    {
        var problem = GroupMedia.CleanVideos([url], out var clean);

        Assert.Equal(fine, problem is null);
        Assert.Equal(fine ? [url] : null, clean);
    }

    [Fact]
    public void Videos_are_tidied_limited_and_may_be_none()
    {
        Assert.Null(GroupMedia.CleanVideos(null, out var none)); Assert.Null(none);
        Assert.Null(GroupMedia.CleanVideos(["", "  "], out var blank)); Assert.Null(blank);
        Assert.Null(GroupMedia.CleanVideos(["https://youtu.be/a", " https://youtu.be/a ", "https://youtu.be/b"], out var two));
        Assert.Equal(["https://youtu.be/a", "https://youtu.be/b"], two);
        Assert.Contains("up to 5", GroupMedia.CleanVideos(Enumerable.Range(0, 6).Select(i => $"https://youtu.be/{i}"), out _));
        Assert.NotNull(GroupMedia.CleanVideos(["https://youtu.be/a", "https://vimeo.com/b"], out _));   // one bad link refuses all
    }
}

using ReservEase.Alumni.Common.Sdk.Text;

namespace ReservEase.Alumni.Common.Tests;

public class RichTextTests
{
    // ── Nothing that can run code survives ──────────────────────────────

    [Theory]
    [InlineData("<p>Hi</p><script>alert(1)</script>", "alert")]
    [InlineData("<p onclick=\"alert(1)\">Hi</p>", "onclick")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<p>Hi</p><iframe src=\"https://evil.example\"></iframe>", "iframe")]
    [InlineData("<a href=\"javascript:alert(1)\">click</a>", "javascript")]
    [InlineData("<a href=\" JaVaScRiPt:alert(1)\">click</a>", "script:")]
    [InlineData("<a href=\"data:text/html,<script>alert(1)</script>\">x</a>", "data:")]
    [InlineData("<a href=\"vbscript:msgbox(1)\">x</a>", "vbscript")]
    [InlineData("<p style=\"background:url(javascript:alert(1))\">x</p>", "javascript")]
    [InlineData("<p style=\"position:fixed;top:0;left:0;width:100vw;height:100vh\">cover</p>", "position")]
    [InlineData("<svg onload=alert(1)><circle/></svg>", "onload")]
    [InlineData("<form action=\"https://evil.example\"><input name=password></form>", "form")]
    [InlineData("<style>body{display:none}</style>", "display")]
    [InlineData("<object data=\"x\"></object><embed src=\"x\">", "embed")]
    [InlineData("<p>x</p><!--[if IE]><script>alert(1)</script><![endif]-->", "alert")]
    [InlineData("<SCRIPT SRC=//evil.example/x.js></SCRIPT>", "evil")]
    [InlineData("<math><mtext><table><mglyph><style><img src=x onerror=alert(1)>", "onerror")]
    public void Dangerous_markup_never_survives_sanitising(string input, string mustNotAppear)
    {
        var result = RichText.Sanitize(input) ?? "";
        Assert.DoesNotContain(mustNotAppear, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_link_opens_in_a_new_tab_without_an_opener_and_only_web_addresses_are_kept()
    {
        var html = RichText.Sanitize("<p><a href=\"https://example.com/page\">site</a> <a href=\"mailto:a@b.co\">mail</a> <a href=\"tel:+233200000000\">call</a></p>")!;
        Assert.Contains("href=\"https://example.com/page\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
        Assert.Contains("mailto:a@b.co", html);
        Assert.Contains("tel:+233200000000", html);
    }

    [Fact]
    public void A_link_cannot_set_its_own_target_or_rel_to_something_else()
    {
        var html = RichText.Sanitize("<a href=\"https://example.com\" target=\"_top\" rel=\"opener\">x</a>")!;
        Assert.Contains("target=\"_blank\"", html);
        Assert.DoesNotContain("_top", html);
        Assert.DoesNotContain("rel=\"opener\"", html);
    }

    // ── The formatting the editor makes survives ────────────────────────

    [Theory]
    [InlineData("<p>Hello <strong>bold</strong>, <em>italic</em>, <u>underlined</u> and <s>struck</s>.</p>")]
    [InlineData("<h2>Programme</h2><h3>Morning</h3><p>Registration</p>")]
    [InlineData("<ul><li>One</li><li>Two</li></ul><ol><li>First</li><li>Second</li></ol>")]
    [InlineData("<blockquote><p>A quote</p></blockquote><hr>")]
    [InlineData("<p style=\"text-align: center\">Centred</p>")]
    public void Normal_formatting_is_kept(string input)
    {
        var result = RichText.Sanitize(input)!;
        Assert.Equal(RichText.ToPlainText(input), RichText.ToPlainText(result));
        foreach (var tag in new[] { "strong", "em", "u", "s", "h2", "h3", "ul", "ol", "li", "blockquote", "hr" }.Where(t => input.Contains($"<{t}")))
            Assert.Contains($"<{tag}", result);
        if (input.Contains("text-align")) Assert.Contains("text-align: center", result.Replace("text-align:center", "text-align: center"));
    }

    [Fact]
    public void Only_alignment_is_allowed_as_a_style()
    {
        var result = RichText.Sanitize("<p style=\"text-align:right;color:red;font-size:99px\">x</p>")!;
        Assert.Contains("text-align", result);
        Assert.DoesNotContain("color", result);
        Assert.DoesNotContain("font-size", result);
    }

    // ── Text from before the editor existed ─────────────────────────────

    [Fact]
    public void Plain_text_becomes_paragraphs_and_line_breaks()
    {
        var html = RichText.Sanitize("First paragraph.\nStill the first.\n\nSecond paragraph.")!;
        Assert.Equal("<p>First paragraph.<br>Still the first.</p><p>Second paragraph.</p>", html);
    }

    [Fact]
    public void Plain_text_is_escaped_not_trusted_as_markup()
    {
        var html = RichText.Sanitize("Use <b>caution</b> when x < y & z > w")!;   // "<b>" is markup here, so it is sanitised as HTML
        Assert.Contains("<b>caution</b>", html);
        var math = RichText.Sanitize("Bring 2 < 3 chairs & 5 > 4 tables")!;      // no markup: plain text, escaped
        Assert.Equal("<p>Bring 2 &lt; 3 chairs &amp; 5 &gt; 4 tables</p>", math);
    }

    [Fact]
    public void Plain_text_that_looks_like_an_attack_is_only_ever_text()
    {
        var html = RichText.Sanitize("Hello\n\nvisit javascript:alert(1) today")!;
        Assert.DoesNotContain("<a", html);
        Assert.Contains("javascript:alert(1)", html);   // shown as words, harmless as text
    }

    // ── Empty means nothing ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("<p></p>")]
    [InlineData("<p> </p><p><br></p>")]
    [InlineData("<script>alert(1)</script>")]
    public void Nothing_worth_saving_becomes_null(string? input) => Assert.Null(RichText.Sanitize(input));

    [Fact]
    public void A_divider_alone_still_counts_as_content() => Assert.NotNull(RichText.Sanitize("<hr>"));

    [Fact]
    public void Sanitising_twice_changes_nothing()
    {
        var once = RichText.Sanitize("<h2>Hi</h2><p>One <a href=\"https://example.com\">link</a></p><ul><li>a</li></ul>")!;
        Assert.Equal(once, RichText.Sanitize(once));
    }

    // ── Plain text for places that can't show formatting ────────────────

    [Fact]
    public void Plain_text_puts_paragraphs_and_list_items_on_their_own_lines_and_drops_the_tags()
    {
        var text = RichText.ToPlainText("<h2>Programme</h2><p>Doors open at <strong>5pm</strong>.</p><ul><li>Tea</li><li>Talks</li></ul><p>See &amp; hear&nbsp;you!</p>");
        Assert.Equal("Programme\n\nDoors open at 5pm.\n\n• Tea\n• Talks\n\nSee & hear you!", text);
    }

    [Fact]
    public void Plain_text_never_contains_script_content()
        => Assert.DoesNotContain("alert", RichText.ToPlainText("<p>Hi</p><script>alert(1)</script>"));

    [Fact]
    public void Plain_text_passes_through_unchanged_and_blank_is_empty()
    {
        Assert.Equal("Just words, 2 < 3.", RichText.ToPlainText("  Just words, 2 < 3.  "));
        Assert.Equal("", RichText.ToPlainText(null));
        Assert.Equal("", RichText.ToPlainText("<p></p>"));
    }

    [Fact]
    public void An_excerpt_is_cut_at_a_word_and_marked()
    {
        var text = RichText.Excerpt("<p>The annual homecoming dinner brings classmates together for an evening of food and awards.</p>", 40);
        Assert.EndsWith("…", text);
        Assert.True(text.Length <= 41);
        Assert.DoesNotContain("<", text);
        Assert.Equal("Short.", RichText.Excerpt("<p>Short.</p>", 40));
    }

    [Fact]
    public void Absurdly_large_text_is_reported_as_too_long()
    {
        Assert.False(RichText.IsTooLong(RichText.Sanitize("A normal description.")));
        Assert.True(RichText.IsTooLong("<p>" + new string('x', RichText.MaxPlainLength + 1) + "</p>"));
        Assert.True(RichText.IsTooLong("<p>x</p>" + new string(' ', RichText.MaxHtmlLength)));
        Assert.False(RichText.IsTooLong(null));
    }
}

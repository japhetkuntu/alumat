using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ReservEase.Alumni.Common.Sdk.Text;

/// <summary>
/// Descriptions written with the rich-text editor (headings, bold, lists, links…) are stored as a small, safe subset of HTML.
/// <see cref="Sanitize"/> is the only way such text should be saved: it keeps the formatting the editor can make and removes
/// everything else (scripts, event handlers, styles, iframes, unsafe links), so stored content can never run code in someone's browser.
/// <see cref="ToPlainText"/> gives the words alone for the places that can't show formatting (SMS, WhatsApp, email previews, search).
/// Text saved before the editor existed is plain; it is treated as paragraphs, never as HTML.
/// </summary>
public static class RichText
{
    /// <summary>The largest saved description, in characters of HTML. Generous for a long page of formatted text, small enough to stop abuse.</summary>
    public const int MaxHtmlLength = 60_000;

    /// <summary>The most words-worth of text (tags excluded) a description may hold.</summary>
    public const int MaxPlainLength = 20_000;

    public const string TooLongMessage = "That description is too long. Please shorten it a little.";

    // Anything shaped like a tag or a comment counts as markup and goes through the sanitiser, which removes whatever is not allowed.
    // Needing a letter right after "<" keeps ordinary text like "2 < 3" or "<3" or "<jane@x.com>" as plain text.
    private static readonly Regex HtmlTag = new(
        @"<\s*/?\s*[a-zA-Z][a-zA-Z0-9:-]*(\s[^<>]*)?/?\s*>|<!--|<\?",
        RegexOptions.Compiled);

    private static HtmlSanitizer Build()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in new[] { "p", "br", "strong", "b", "em", "i", "u", "s", "strike", "del", "ul", "ol", "li", "h1", "h2", "h3", "h4", "blockquote", "hr", "a", "code", "pre" })
            s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        foreach (var a in new[] { "href", "target", "rel", "style" }) s.AllowedAttributes.Add(a);
        // The only styling the editor needs is alignment; any other style property is removed.
        s.AllowedCssProperties.Clear();
        s.AllowedCssProperties.Add("text-align");
        s.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto", "tel" }) s.AllowedSchemes.Add(scheme);
        s.KeepChildNodes = false;
        // Every link opens in a new tab and carries no opener or referrer.
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement el && el.TagName.Equals("A", StringComparison.OrdinalIgnoreCase) && el.HasAttribute("href"))
            {
                el.SetAttribute("target", "_blank");
                el.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };
        return s;
    }

    // HtmlSanitizer is safe to share once configured.
    private static readonly HtmlSanitizer Sanitizer = Build();

    /// <summary>Whether the text already contains markup the editor writes. Plain text with a stray "&lt;" does not count.</summary>
    public static bool LooksLikeHtml(string? text) => !string.IsNullOrEmpty(text) && HtmlTag.IsMatch(text);

    /// <summary>Plain text as paragraphs: blank lines split paragraphs, single line breaks become &lt;br&gt;, everything is escaped.</summary>
    public static string PlainToHtml(string text)
    {
        var paragraphs = Regex.Split(text.Replace("\r\n", "\n").Replace('\r', '\n').Trim(), @"\n{2,}")
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Concat(paragraphs.Select(p => "<p>" + WebUtility.HtmlEncode(p.Trim()).Replace("\n", "<br>") + "</p>"));
    }

    /// <summary>
    /// The safe form of a description to store. Null for nothing worth saving (blank, or an editor's empty paragraph). Plain text becomes
    /// paragraphs; HTML is reduced to the allowed subset. Check <see cref="IsTooLong"/> on the result if a limit matters.
    /// </summary>
    public static string? Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var html = LooksLikeHtml(input) ? Sanitizer.Sanitize(input) : PlainToHtml(input);
        html = html.Trim();
        // An empty editor saves "<p></p>"; a document that is only a divider is still content.
        if (ToPlainText(html).Length == 0 && !html.Contains("<hr", StringComparison.OrdinalIgnoreCase)) return null;
        return html;
    }

    /// <summary>True when a sanitised description is bigger than the limits above.</summary>
    public static bool IsTooLong(string? html) => html is not null && (html.Length > MaxHtmlLength || ToPlainText(html).Length > MaxPlainLength);

    /// <summary>The words alone: paragraphs and list items on their own lines, tags and entities gone. Plain text passes through unchanged.</summary>
    public static string ToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        if (!LooksLikeHtml(input)) return input.Trim();

        var s = input;
        s = Regex.Replace(s, @"<(script|style)\b[^>]*>.*?</\1\s*>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        s = Regex.Replace(s, @"<!--.*?-->", " ", RegexOptions.Singleline);
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<hr\s*/?>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<li\b[^>]*>", "• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</li\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</(ul|ol)\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</(p|h[1-6]|blockquote|div|pre|table)\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]*>", string.Empty);
        s = WebUtility.HtmlDecode(s);
        var lines = s.Replace("\r", "").Split('\n').Select(l => Regex.Replace(l, @"[ \t\u00A0]+", " ").Trim());
        return Regex.Replace(string.Join("\n", lines), @"\n{3,}", "\n\n").Trim();
    }

    /// <summary>A short plain-text taste of a description for a card, message or preview, cut at a word and ended with "…".</summary>
    public static string Excerpt(string? input, int maxLength = 200)
    {
        var text = Regex.Replace(ToPlainText(input), @"\s+", " ").Trim();
        if (text.Length <= maxLength) return text;
        var cut = text[..maxLength];
        var space = cut.LastIndexOf(' ');
        return (space > maxLength / 2 ? cut[..space] : cut).TrimEnd(' ', ',', '.', ';', ':', '-') + "…";
    }
}

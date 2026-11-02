using System.Text.Json;
using System.Text.RegularExpressions;
using ReservEase.Alumni.Common.Sdk.Text;

namespace ReservEase.Alumni.PostgresDb.Sdk.Website;

public sealed record WebsiteValidationResult(WebsiteDocument? Document, string? Json, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0 && Document is not null;
}

/// <summary>
/// The only way a website page is allowed into the database. It reads the JSON the editor sends, keeps what is known and safe, and
/// returns a cleaned copy: text stripped of markup (rich text reduced to a small allowed set), links limited to a few safe kinds, photos
/// limited to https addresses, colours checked, and sizes capped. A page can therefore never carry script or markup that runs in a visitor's browser.
/// </summary>
public static class WebsiteDocumentValidator
{
    public const int MaxJsonLength = 300_000;
    public const int MaxSections = 40;
    private const int MaxUrlLength = 1000;

    private static readonly Regex Id = new(@"^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);
    private static readonly Regex SlotKey = new(@"^[A-Za-z0-9_-]{1,60}$", RegexOptions.Compiled);
    private static readonly Regex Hex = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$", RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"^\+?[0-9][0-9\s\-().]{3,24}$", RegexOptions.Compiled);

    public static WebsiteValidationResult Validate(string? json, ISet<string>? disabledFeatures = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return Fail("There is nothing to save.");
        if (json.Length > MaxJsonLength) return Fail("This page is too big to save. Remove a few sections or photos.");

        WebsiteDocument? raw;
        try { raw = JsonSerializer.Deserialize<WebsiteDocument>(json, WebsiteDocument.Json); }
        catch (JsonException) { return Fail("That page could not be read. Reload the editor and try again."); }
        if (raw is null) return Fail("That page could not be read. Reload the editor and try again.");

        var errors = new List<string>();
        var doc = Clean(raw, disabledFeatures, errors);
        if (errors.Count > 0) return new WebsiteValidationResult(null, null, errors);
        return new WebsiteValidationResult(doc, JsonSerializer.Serialize(doc, WebsiteDocument.Json), []);
    }

    /// <summary>What must also be true before a page can go live. Everything else can be fixed later, so it never blocks.</summary>
    public static IReadOnlyList<string> ProblemsBeforePublishing(WebsiteDocument doc)
    {
        var problems = new List<string>();
        if (!doc.Sections.Any(s => !s.Hidden)) problems.Add("Add at least one section that is not hidden before publishing.");
        // Words the builder puts in a new section so it is not blank. They are never meant to reach visitors.
        var waiting = doc.Sections.Where(s => !s.Hidden && HoldsPlaceholder(s)).Select(s => s.Type).Distinct().ToList();
        if (waiting.Count > 0) problems.Add("Some sections still have the words the builder started them with (such as “Say a little about it here”). Replace or remove them before publishing.");
        return problems;
    }

    /// <summary>The starter wording of a new section. Keep in step with PLACEHOLDERS in the site package.</summary>
    public static readonly string[] PlaceholderWords = ["Say a little about it here.", "Full name", "New question", "Write the answer here.", "New card", "Add your words here."];

    private static bool HoldsPlaceholder(WebsiteSection s)
        => s.Props.Values.Concat((s.Items ?? []).SelectMany(i => i.Fields.Values)).Any(v => PlaceholderWords.Any(p => v.Contains(p, StringComparison.Ordinal)));

    private static WebsiteValidationResult Fail(string message) => new(null, null, [message]);

    private static WebsiteDocument Clean(WebsiteDocument raw, ISet<string>? disabled, List<string> errors)
    {
        var doc = new WebsiteDocument
        {
            SchemaVersion = 1,
            Template = Pick(raw.Template, WebsiteBlocks.Templates, "vibrant"),
            Design = CleanDesign(raw.Design ?? new()),
            Footer = CleanFooter(raw.Footer ?? new()),
            Seo = new WebsiteSeo { Title = Plain(raw.Seo?.Title, 70), Description = Plain(raw.Seo?.Description, 200), ShareImage = Url(raw.Seo?.ShareImage) },
        };

        foreach (var f in WebsiteBlocks.WebsiteFeatures)
            doc.Features[f] = raw.Features is not null && raw.Features.TryGetValue(f, out var on) && on;

        var sections = raw.Sections ?? [];
        if (sections.Count > MaxSections) { errors.Add($"A page can have at most {MaxSections} sections."); return doc; }
        var ids = new HashSet<string>();
        foreach (var s in sections)
        {
            if (s is null) continue;
            if (!Id.IsMatch(s.Id ?? "")) { errors.Add("A section is missing its identifier. Reload the editor."); continue; }
            if (!ids.Add(s.Id)) { errors.Add("Two sections share the same identifier. Reload the editor."); continue; }
        }
        if (errors.Count > 0) return doc;

        foreach (var s in sections.Where(s => s is not null))
        {
            if (!WebsiteBlocks.All.TryGetValue(s.Type ?? "", out var block)) { errors.Add($"“{Plain(s.Type, 30)}” is not a section this website can show."); continue; }
            doc.Sections.Add(CleanSection(s, block, ids, disabled));
        }
        return doc;
    }

    private static WebsiteSection CleanSection(WebsiteSection s, WebsiteBlocks.Block block, ISet<string> sectionIds, ISet<string>? disabled)
    {
        var o = new WebsiteSection
        {
            Id = s.Id, Type = s.Type,
            Variant = Pick(s.Variant, block.Variants, block.Variants[0]),
            Bg = Pick(s.Bg, WebsiteBlocks.Backgrounds, "plain"),
            Space = Pick(s.Space, WebsiteBlocks.Spacings, "n"),
            Hidden = s.Hidden,
            Count = Math.Clamp(s.Count, 1, 12),
            Menu = new WebsiteMenuEntry { On = s.Menu?.On ?? false, Label = Plain(s.Menu?.Label, 22) },
        };
        // The platform can switch a feature off for an institution; its section must never reach visitors, whatever the page says.
        if (block.Feature is { } feature && disabled is not null && disabled.Contains(feature)) o.Hidden = true;

        foreach (var key in block.PropKeys)
        {
            if (s.Props is null || !s.Props.TryGetValue(key, out var v)) continue;
            o.Props[key] = block.RichKeys.Contains(key) ? Rich(v) : Plain(v, key is "heading" or "heading2" ? 140 : key is "body" or "body2" ? 600 : 200);
        }
        foreach (var (slot, url) in s.Imgs ?? [])
            if (SlotKey.IsMatch(slot) && Url(url) is { } clean) o.Imgs[slot] = clean;
        foreach (var key in block.LinkKeys)
            if (s.Links is not null && s.Links.TryGetValue(key, out var l)) o.Links[key] = CleanLink(l, sectionIds);

        if (block.ItemFields.Length > 0)
        {
            o.Items = [];
            var seen = new HashSet<string>();
            foreach (var it in (s.Items ?? []).Where(i => i is not null).Take(block.MaxItems))
            {
                if (!Id.IsMatch(it.Id ?? "") || !seen.Add(it.Id)) continue;
                var item = new WebsiteItem { Id = it.Id };
                foreach (var key in block.ItemFields)
                    if (it.Fields is not null && it.Fields.TryGetValue(key, out var v))
                        item.Fields[key] = block.RichItemFields.Contains(key) ? Rich(v) : Plain(v, key is "text" or "a" ? 600 : 200);
                foreach (var key in block.ItemLinkKeys)
                    if (it.Links is not null && it.Links.TryGetValue(key, out var l)) item.Links[key] = CleanLink(l, sectionIds);
                o.Items.Add(item);
            }
            // A photo slot for an item that no longer exists is dropped, so removing an item never leaves its photo behind.
            foreach (var slot in o.Imgs.Keys.Where(k => k.StartsWith('i') && k.Length > 1 && !seen.Contains(k[1..])).ToList())
                if (block.ItemFields.Length > 0 && !IsFixedSlot(slot)) o.Imgs.Remove(slot);
        }
        return o;
    }

    private static bool IsFixedSlot(string slot) => slot is "main" or "bg" or "map" or "a" or "b" or "c";

    private static WebsiteLink CleanLink(WebsiteLink? l, ISet<string> sectionIds)
    {
        var kind = Pick(l?.T, WebsiteBlocks.LinkKinds, "none");
        var v = l?.V?.Trim();
        switch (kind)
        {
            case "anchor": return !string.IsNullOrEmpty(v) && sectionIds.Contains(v) ? new() { T = "anchor", V = v } : new();
            case "join": case "signin": return new() { T = kind };
            case "external":
                var u = Url(v, allowHttp: true);
                return u is null ? new() : new() { T = "external", V = u };
            case "email": return v is not null && v.Length <= 120 && Email.IsMatch(v) ? new() { T = "email", V = v } : new();
            case "phone": return v is not null && Phone.IsMatch(v) ? new() { T = "phone", V = v } : new();
            default: return new();
        }
    }

    private static WebsiteDesign CleanDesign(WebsiteDesign d) => new()
    {
        UseBrand = d.UseBrand,
        Primary = d.Primary is not null && Hex.IsMatch(d.Primary) ? d.Primary.ToLowerInvariant() : null,
        LogoUrl = Url(d.LogoUrl),
        Name = NullIfEmpty(Plain(d.Name, 80)),
        Font = Pick(d.Font, WebsiteBlocks.Fonts, "vibrant"),
        Corners = Pick(d.Corners, WebsiteBlocks.Corners, "26px"),
        Dark = d.Dark,
        Vibe = d.Vibe,
        HeaderStyle = Pick(d.HeaderStyle, WebsiteBlocks.HeaderStyles, "solid"),
        Sticky = d.Sticky,
    };

    private static WebsiteFooter CleanFooter(WebsiteFooter f)
    {
        var o = new WebsiteFooter
        {
            Text = NullIfEmpty(Plain(f.Text, 200)),
            Email = f.Email is not null && Email.IsMatch(f.Email.Trim()) && f.Email.Length <= 120 ? f.Email.Trim() : null,
            Phone = f.Phone is not null && Phone.IsMatch(f.Phone.Trim()) ? f.Phone.Trim() : null,
            Addr = NullIfEmpty(Plain(f.Addr, 200)),
            Menu = f.Menu, Credit = f.Credit,
            Style = Pick(f.Style, WebsiteBlocks.FooterStyles, "dark"),
        };
        foreach (var key in WebsiteBlocks.SocialKeys)
            if (f.Social is not null && f.Social.TryGetValue(key, out var v) && Url(v, allowHttp: false) is { } u) o.Social[key] = u;
        return o;
    }

    // ── Text and addresses ─────────────────────────────────────────────────────────────────────────

    /// <summary>Words only: tags removed, control characters and runs of blanks collapsed, cut at a limit.</summary>
    public static string Plain(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var text = RichText.LooksLikeHtml(s) ? RichText.ToPlainText(s) : s;
        text = Regex.Replace(text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]", "");
        text = Regex.Replace(text, @"[ \t]{2,}", " ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    /// <summary>A little formatting kept (bold, italic, lists, links); everything else removed.</summary>
    public static string Rich(string? s)
    {
        var html = RichText.Sanitize(s);
        if (html is null) return string.Empty;
        return html.Length <= 6000 ? html : string.Empty;
    }

    /// <summary>An https address (http too where a link may reach an old site). Anything else, including javascript: and data:, is dropped.</summary>
    public static string? Url(string? s, bool allowHttp = false)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.Length > MaxUrlLength) return null;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return null;
        if (!string.IsNullOrEmpty(u.UserInfo)) return null;
        // An address on the developer's own machine may be plain http (local file storage); it can only ever reach that machine.
        var loopback = u.Scheme == Uri.UriSchemeHttp && (u.Host is "localhost" or "127.0.0.1" or "[::1]");
        var ok = u.Scheme == Uri.UriSchemeHttps || loopback || (allowHttp && u.Scheme == Uri.UriSchemeHttp);
        return ok ? u.ToString() : null;
    }

    private static string Pick(string? value, string[] allowed, string fallback)
        => value is not null && allowed.Contains(value, StringComparer.Ordinal) ? value : fallback;

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
}

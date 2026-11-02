using System.Text.Json;
using ReservEase.Alumni.PostgresDb.Sdk.Website;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>A website page is data, never code: whatever the editor sends is cleaned before it can be stored or shown to visitors.</summary>
public class WebsiteDocumentValidatorTests
{
    private static string Page(params object[] sections) => JsonSerializer.Serialize(new { sections }, WebsiteDocument.Json);
    private static object Section(string id, string type = "text", object? props = null, object? imgs = null, object? links = null, object? items = null, string variant = "", string bg = "plain", int count = 3, bool hidden = false)
        => new { id, type, variant, bg, space = "n", hidden, count, menu = new { on = false, label = "" }, props = props ?? new { }, imgs = imgs ?? new { }, links = links ?? new { }, items };

    private static WebsiteDocument Ok(string json, ISet<string>? disabled = null)
    {
        var r = WebsiteDocumentValidator.Validate(json, disabled);
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        return r.Document!;
    }

    // ── What is refused ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("{ not json")] [InlineData("[1,2]")]
    public void Nothing_or_unreadable_is_refused_with_a_plain_message(string? json)
    {
        var r = WebsiteDocumentValidator.Validate(json);
        Assert.False(r.IsValid);
        Assert.NotEmpty(r.Errors);
        Assert.DoesNotContain("Exception", r.Errors[0]);
    }

    [Fact]
    public void A_page_that_is_far_too_big_is_refused()
        => Assert.False(WebsiteDocumentValidator.Validate(new string('x', WebsiteDocumentValidator.MaxJsonLength + 1)).IsValid);

    [Fact]
    public void An_unknown_kind_of_section_is_refused_so_nothing_unexpected_can_be_shown()
    {
        var r = WebsiteDocumentValidator.Validate(Page(Section("a", "script")));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Too_many_sections_or_a_repeated_identifier_is_refused()
    {
        Assert.False(WebsiteDocumentValidator.Validate(Page(Enumerable.Range(0, WebsiteDocumentValidator.MaxSections + 1).Select(i => Section($"s{i}")).ToArray())).IsValid);
        Assert.False(WebsiteDocumentValidator.Validate(Page(Section("same"), Section("same"))).IsValid);
        Assert.False(WebsiteDocumentValidator.Validate(Page(Section("has space"))).IsValid);
    }

    // ── Text cannot carry markup ────────────────────────────────────────

    [Fact]
    public void Plain_text_loses_any_markup()
    {
        var doc = Ok(Page(Section("a", "text", props: new { heading = "<img src=x onerror=alert(1)>Welcome <b>home</b>" })));
        var heading = doc.Sections[0].Props["heading"];
        Assert.DoesNotContain("<", heading);
        Assert.Contains("Welcome", heading);
    }

    [Fact]
    public void Rich_text_keeps_a_little_formatting_but_never_scripts_or_event_handlers()
    {
        var doc = Ok(Page(Section("a", "text", props: new { body = "Hello <b>there</b><script>alert(1)</script> <a href=\"javascript:alert(1)\" onclick=\"x()\">bad</a> <a href=\"https://example.org\">ok</a>" })));
        var body = doc.Sections[0].Props["body"];
        Assert.Contains("<b>there</b>", body);
        Assert.DoesNotContain("script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.org", body);
    }

    [Fact]
    public void Long_text_is_cut_to_its_limit()
    {
        var doc = Ok(Page(Section("a", "text", props: new { heading = new string('h', 500) })));
        Assert.Equal(140, doc.Sections[0].Props["heading"].Length);
    }

    [Fact]
    public void Properties_a_section_does_not_have_are_dropped()
    {
        var doc = Ok(Page(Section("a", "stats", props: new { heading = "x", evil = "y" })));
        Assert.Empty(doc.Sections[0].Props);
    }

    // ── Links and photos ────────────────────────────────────────────────

    [Fact]
    public void A_button_can_only_go_to_a_safe_place()
    {
        var doc = Ok(Page(
            Section("hero1", "hero", links: new { b1 = new { t = "external", v = "javascript:alert(1)" }, b2 = new { t = "external", v = "https://example.org/give" } }),
            Section("hero2", "hero", links: new { b1 = new { t = "anchor", v = "nope" }, b2 = new { t = "anchor", v = "hero1" } }),
            Section("hero3", "hero", links: new { b1 = new { t = "email", v = "not an email" }, b2 = new { t = "phone", v = "+233 20 000 0000" } })));
        Assert.Equal("none", doc.Sections[0].Links["b1"].T);
        Assert.Equal("https://example.org/give", doc.Sections[0].Links["b2"].V);
        Assert.Equal("none", doc.Sections[1].Links["b1"].T);       // a section that does not exist
        Assert.Equal("hero1", doc.Sections[1].Links["b2"].V);
        Assert.Equal("none", doc.Sections[2].Links["b1"].T);
        Assert.Equal("phone", doc.Sections[2].Links["b2"].T);
    }

    [Fact]
    public void Only_https_photos_are_kept()
    {
        var doc = Ok(Page(Section("a", "hero", imgs: new { main = "https://images.unsplash.com/photo-1?w=900", bg = "http://insecure.example/x.jpg", map = "javascript:alert(1)", i1 = "data:image/svg+xml;base64,AAAA" })));
        Assert.Equal(["main"], doc.Sections[0].Imgs.Keys);
    }

    [Fact]
    public void Plain_http_is_only_kept_for_an_address_on_the_developers_own_machine()
    {
        Assert.NotNull(WebsiteDocumentValidator.Url("http://localhost:9000/bucket/a.jpg"));
        Assert.NotNull(WebsiteDocumentValidator.Url("http://127.0.0.1:9000/a.jpg"));
        Assert.Null(WebsiteDocumentValidator.Url("http://images.example.org/a.jpg"));
        Assert.Null(WebsiteDocumentValidator.Url("http://localhost.evil.example/a.jpg"));
    }

    [Fact]
    public void An_address_with_a_login_embedded_is_dropped()
        => Assert.Null(WebsiteDocumentValidator.Url("https://user:pass@example.org/x"));

    // ── Shape and defaults ──────────────────────────────────────────────

    [Fact]
    public void Unknown_choices_fall_back_to_safe_defaults()
    {
        var doc = Ok(Page(Section("a", "hero", variant: "sideways", bg: "neon", count: 99)));
        Assert.Equal("split", doc.Sections[0].Variant);
        Assert.Equal("plain", doc.Sections[0].Bg);
        Assert.Equal(12, doc.Sections[0].Count);
    }

    [Fact]
    public void Design_values_are_checked()
    {
        var json = JsonSerializer.Serialize(new { sections = new object[] { Section("a") }, design = new { useBrand = false, primary = "red; background:url(x)", logoUrl = "http://x/logo.png", font = "comic", corners = "99px", name = "<b>Greenfield</b>" } }, WebsiteDocument.Json);
        var d = Ok(json).Design;
        Assert.False(d.UseBrand);
        Assert.Null(d.Primary);
        Assert.Null(d.LogoUrl);
        Assert.Equal("vibrant", d.Font);
        Assert.Equal("26px", d.Corners);
        Assert.Equal("Greenfield", d.Name);
        var good = JsonSerializer.Serialize(new { sections = new object[] { Section("a") }, design = new { useBrand = false, primary = "#1F7F78" } }, WebsiteDocument.Json);
        Assert.Equal("#1f7f78", Ok(good).Design.Primary);
    }

    [Fact]
    public void The_footer_keeps_real_contact_details_and_https_social_links_only()
    {
        var json = JsonSerializer.Serialize(new { sections = new object[] { Section("a") }, footer = new { email = "hello@school.edu", phone = "+233 20 000 0000", social = new { facebook = "https://facebook.com/x", instagram = "javascript:alert(1)", myspace = "https://myspace.com/x" } } }, WebsiteDocument.Json);
        var f = Ok(json).Footer;
        Assert.Equal("hello@school.edu", f.Email);
        Assert.Equal(["facebook"], f.Social.Keys);
    }

    // ── Items ───────────────────────────────────────────────────────────

    [Fact]
    public void Items_keep_their_own_fields_and_a_removed_items_photo_goes_with_it()
    {
        var items = new object[] { new { id = "q1", fields = new { q = "Who can join?", a = "<b>Anyone</b>", junk = "x" } }, new { id = "q2", fields = new { q = "Dues?" } } };
        var doc = Ok(Page(Section("faq", "faq", items: items)));
        Assert.Equal(2, doc.Sections[0].Items!.Count);
        Assert.False(doc.Sections[0].Items![0].Fields.ContainsKey("junk"));

        var withPhotos = Ok(Page(Section("cards", "cards", variant: "steps",
            imgs: new { iq1 = "https://images.unsplash.com/photo-1", iGone = "https://images.unsplash.com/photo-2" },
            items: new object[] { new { id = "q1", fields = new { title = "One" } } })));
        Assert.Equal(["iq1"], withPhotos.Sections[0].Imgs.Keys);
    }

    [Fact]
    public void A_section_without_items_has_none()
        => Assert.Null(Ok(Page(Section("a", "text", items: new object[] { new { id = "x" } }))).Sections[0].Items);

    // ── The platform switching a feature off ────────────────────────────

    [Fact]
    public void A_section_for_a_feature_the_platform_switched_off_is_hidden_whatever_the_page_says()
    {
        var json = Page(Section("s", "services", hidden: false), Section("n", "news", hidden: false), Section("t", "text"));
        var doc = Ok(json, new HashSet<string> { "Services" });
        Assert.True(doc.Sections.Single(s => s.Id == "s").Hidden);
        Assert.False(doc.Sections.Single(s => s.Id == "n").Hidden);
        Assert.False(doc.Sections.Single(s => s.Id == "t").Hidden);
    }

    [Fact]
    public void Only_the_known_website_features_can_be_switched()
    {
        var json = JsonSerializer.Serialize(new { sections = new object[] { Section("a") }, features = new Dictionary<string, bool> { ["Store"] = true, ["Hacking"] = true } }, WebsiteDocument.Json);
        var f = Ok(json).Features;
        Assert.True(f["Store"]);
        Assert.False(f["Services"]);
        Assert.False(f.ContainsKey("Hacking"));
    }

    // ── Stable and publishable ──────────────────────────────────────────

    [Fact]
    public void Cleaning_a_cleaned_page_changes_nothing()
    {
        var first = WebsiteDocumentValidator.Validate(Page(Section("a", "hero", props: new { heading = "Hi <b>x</b>", body = "Some <i>words</i>" }), Section("b", "events")));
        var second = WebsiteDocumentValidator.Validate(first.Json);
        Assert.Equal(first.Json, second.Json);
    }

    [Fact]
    public void A_page_that_still_has_the_builders_starter_words_cannot_go_live_until_they_are_changed_or_hidden()
    {
        var starter = Ok(Page(Section("a", "cards", items: new object[] { new { id = "c1", fields = new { title = "New card", text = "Say a little about it here." } } })));
        Assert.NotEmpty(WebsiteDocumentValidator.ProblemsBeforePublishing(starter));
        var hiddenStarter = Ok(Page(Section("a", "cards", hidden: true, items: new object[] { new { id = "c1", fields = new { title = "New card" } } }), Section("b")));
        Assert.Empty(WebsiteDocumentValidator.ProblemsBeforePublishing(hiddenStarter));
        var written = Ok(Page(Section("a", "cards", items: new object[] { new { id = "c1", fields = new { title = "Mentorship", text = "Meet someone ten years ahead." } } })));
        Assert.Empty(WebsiteDocumentValidator.ProblemsBeforePublishing(written));
    }

    [Fact]
    public void A_page_needs_at_least_one_visible_section_to_go_live()
    {
        Assert.NotEmpty(WebsiteDocumentValidator.ProblemsBeforePublishing(Ok(Page(Section("a", hidden: true)))));
        Assert.NotEmpty(WebsiteDocumentValidator.ProblemsBeforePublishing(Ok(Page())));
        Assert.Empty(WebsiteDocumentValidator.ProblemsBeforePublishing(Ok(Page(Section("a")))));
    }
}

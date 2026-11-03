using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservEase.Alumni.PostgresDb.Sdk.Website;

/// <summary>
/// An institution's website as data: one scrolling page made of sections, never raw HTML or script. The same shape is edited in the
/// institution portal and rendered by the member app, and <see cref="WebsiteDocumentValidator"/> cleans it before it is ever stored.
/// </summary>
public sealed class WebsiteDocument
{
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Which starting look it began from (vibrant, heritage, campus, journal, bold). The look itself lives in <see cref="Design"/>.</summary>
    public string Template { get; set; } = "vibrant";
    public WebsiteDesign Design { get; set; } = new();
    public List<WebsiteSection> Sections { get; set; } = [];
    public WebsiteFooter Footer { get; set; } = new();
    public WebsiteSeo Seo { get; set; } = new();
    /// <summary>Which of the institution's other features show on the website (Services, Store, Fundraisers, Jobs, Businesses, Communities). Off hides the section and its menu entry but keeps every edit.</summary>
    public Dictionary<string, bool> Features { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class WebsiteDesign
{
    /// <summary>On = the website uses the institution's own colours and logo from its portal theme. Turning it off changes the website only, never the portal, emails or certificates.</summary>
    public bool UseBrand { get; set; } = true;
    /// <summary>Only used when <see cref="UseBrand"/> is off.</summary>
    public string? Primary { get; set; }
    /// <summary>Only used when <see cref="UseBrand"/> is off.</summary>
    public string? LogoUrl { get; set; }
    /// <summary>The name shown in the header and footer. Empty = the institution's name.</summary>
    public string? Name { get; set; }
    public string Font { get; set; } = "vibrant";
    public string Corners { get; set; } = "26px";
    public bool Dark { get; set; }
    /// <summary>The full-colour look: rounded section edges, pill buttons, photo arches.</summary>
    public bool Vibe { get; set; } = true;
    public string HeaderStyle { get; set; } = "solid";
    public bool Sticky { get; set; }
}

public sealed class WebsiteSection
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Bg { get; set; } = "plain";
    public string Space { get; set; } = "n";
    public bool Hidden { get; set; }
    public int Count { get; set; } = 3;
    public WebsiteMenuEntry Menu { get; set; } = new();
    public Dictionary<string, string> Props { get; set; } = [];
    /// <summary>Photos by slot name ("main", "bg", "i{itemId}"…). Values are https addresses.</summary>
    public Dictionary<string, string> Imgs { get; set; } = [];
    public List<WebsiteItem>? Items { get; set; }
    public Dictionary<string, WebsiteLink> Links { get; set; } = [];
}

public sealed class WebsiteMenuEntry
{
    public bool On { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class WebsiteItem
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = [];
    public Dictionary<string, WebsiteLink> Links { get; set; } = [];
}

/// <summary>Where a button goes. Only a few safe kinds: a section on this page, the join or sign-in page, a web address, an email or a phone number.</summary>
public sealed class WebsiteLink
{
    public string T { get; set; } = "none";
    public string? V { get; set; }
}

public sealed class WebsiteFooter
{
    public string? Text { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Addr { get; set; }
    public bool Menu { get; set; } = true;
    public string Style { get; set; } = "dark";
    public bool Credit { get; set; } = true;
    public Dictionary<string, string> Social { get; set; } = [];
    /// <summary>When the office is open, in a few plain lines.</summary>
    public string? Hours { get; set; }
    /// <summary>Links the institution wants in the footer, such as its university website or the library.</summary>
    public List<WebsiteFooterLink> Links { get; set; } = [];
}

public sealed class WebsiteFooterLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class WebsiteSeo
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ShareImage { get; set; }
}

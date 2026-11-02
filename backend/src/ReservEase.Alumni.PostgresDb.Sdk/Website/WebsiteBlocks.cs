using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Website;

/// <summary>The fixed list of things a section can be, and what each is allowed to hold. Anything outside it is refused or dropped when a page is saved.</summary>
public static class WebsiteBlocks
{
    public sealed record Block(string[] Variants, string[] PropKeys, string[] RichKeys, string[] ItemFields, string[] RichItemFields, string[] LinkKeys, string[] ItemLinkKeys, string? Feature, string? WebsiteFeature, int MaxItems = 24);

    private static readonly string[] None = [];
    private static readonly string[] Head = ["eyebrow", "heading"];

    public static readonly IReadOnlyDictionary<string, Block> All = new Dictionary<string, Block>
    {
        ["hero"] = new(["split", "full", "center"], ["eyebrow", "heading", "heading2", "body", "b1", "b2"], ["body"], None, None, ["b1", "b2"], None, null, null),
        ["text"] = new(["one", "two"], ["eyebrow", "heading", "body", "body2"], ["body", "body2"], None, None, None, None, null, null),
        ["imagetext"] = new(["left", "right"], ["eyebrow", "heading", "body", "link"], ["body"], None, None, ["link"], None, null, null),
        ["news"] = new(["list", "grid"], Head, None, None, None, None, None, InstitutionFeatures.News, null),
        ["events"] = new(["list", "cards"], Head, None, None, None, None, None, InstitutionFeatures.Events, null),
        ["stats"] = new(["row"], None, None, None, None, None, None, null, null),
        ["gallery"] = new(["grid", "mosaic"], Head, None, None, None, None, None, null, null, 24),
        ["leaders"] = new(["row"], Head, None, ["name", "role"], None, None, None, null, null, 24),
        ["faq"] = new(["list"], Head, None, ["q", "a"], ["a"], None, None, null, null, 24),
        ["cta"] = new(["band"], ["heading", "body", "b1"], ["body"], None, None, ["b1"], None, null, null),
        ["cards"] = new(["photo", "plain", "numbered", "steps"], Head, None, ["title", "text", "btn"], ["text"], None, ["btn"], null, null, 12),
        ["contact"] = new(["split"], ["heading", "addr", "mail", "tel"], None, None, None, None, None, null, null),
        ["services"] = new(["cards", "list"], Head, None, None, None, None, None, InstitutionFeatures.Services, "Services"),
        ["store"] = new(["grid"], Head, None, None, None, None, None, InstitutionFeatures.Store, "Store"),
        ["fundraisers"] = new(["cards"], Head, None, None, None, None, None, InstitutionFeatures.Contributions, "Fundraisers"),
        ["jobs"] = new(["list"], Head, None, None, None, None, None, InstitutionFeatures.Jobs, "Jobs"),
        ["businesses"] = new(["cards"], Head, None, None, None, None, None, InstitutionFeatures.BusinessDirectory, "Businesses"),
        ["communities"] = new(["cards"], Head, None, None, None, None, None, InstitutionFeatures.Communities, "Communities"),
    };

    public static readonly string[] WebsiteFeatures = ["Services", "Store", "Fundraisers", "Jobs", "Businesses", "Communities"];
    public static readonly string[] Fonts = ["vibrant", "heritage", "campus", "journal", "bold"];
    public static readonly string[] Templates = ["vibrant", "heritage", "campus", "journal", "bold"];
    public static readonly string[] Corners = ["0px", "6px", "14px", "26px"];
    public static readonly string[] Backgrounds = ["plain", "tint", "brand"];
    public static readonly string[] Spacings = ["c", "n", "r"];
    public static readonly string[] LinkKinds = ["none", "anchor", "join", "signin", "external", "email", "phone"];
    public static readonly string[] FooterStyles = ["dark", "brand", "light"];
    public static readonly string[] HeaderStyles = ["solid", "overlay"];
    public static readonly string[] SocialKeys = ["facebook", "instagram", "linkedin", "x", "youtube"];
}

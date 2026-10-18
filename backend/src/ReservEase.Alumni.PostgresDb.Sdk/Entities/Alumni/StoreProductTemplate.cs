using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>The kinds of reusable set a store template can be.</summary>
public static class StoreTemplateKinds
{
    /// <summary>A whole product setup: display details, questions, delivery details and stages together.</summary>
    public const string Setup = "Setup";
    /// <summary>Just the questions asked of the buyer.</summary>
    public const string Questions = "Questions";
    /// <summary>Just the delivery or pickup details (and the delivery instructions shown with them).</summary>
    public const string DeliveryDetails = "DeliveryDetails";
    /// <summary>Just the fulfilment stages.</summary>
    public const string Stages = "Stages";
    /// <summary>Just the details shown on the product page (and the price label and stock setting).</summary>
    public const string Details = "Details";
    /// <summary>Just the variant option types and their values, such as Size: S, M, L.</summary>
    public const string Variants = "Variants";

    public static readonly string[] All = [Setup, Questions, DeliveryDetails, Stages, Details, Variants];
    public static bool IsValid(string? kind) => kind is not null && All.Contains(kind);
}

/// <summary>One variant option and the values it can take, e.g. Size with S, M, L.</summary>
public class VariantOptionSet
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
}

/// <summary>
/// A reusable store set an institution saves once and uses in any product: a whole setup, or just the
/// questions, delivery details, stages, display details or variants (see <see cref="Kind"/>). Using one
/// copies it into the product, which can then be edited freely; editing a template never changes
/// products that already used it.
/// </summary>
public class StoreProductTemplate : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>One of <see cref="StoreTemplateKinds"/>. Existing templates are whole setups.</summary>
    public string Kind { get; set; } = StoreTemplateKinds.Setup;

    public string? PriceLabel { get; set; }
    public bool TrackStock { get; set; } = true;
    public string? DeliveryInfo { get; set; }
    public List<StoreDetailItem> Details { get; set; } = [];
    public List<ServiceFieldDefinition> Fields { get; set; } = [];
    public List<ServiceFieldDefinition> DeliveryFields { get; set; } = [];
    public List<string> Stages { get; set; } = [];

    /// <summary>Only used by the Variants kind.</summary>
    public List<VariantOptionSet> VariantOptions { get; set; } = [];
}

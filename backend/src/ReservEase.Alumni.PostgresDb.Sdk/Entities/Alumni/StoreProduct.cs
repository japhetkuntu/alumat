using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>One "label: value" line an institution shows on a product's page (e.g. "Location: Block A", "Check-in: 2pm").</summary>
public class StoreDetailItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Anything an institution wants to sell — merchandise, accommodation, passes,
/// tickets. What members see, what they are asked to fill in and how the
/// order is fulfilled are all configured per product (see <see cref="Details"/>,
/// <see cref="Fields"/>, <see cref="DeliveryFields"/>, <see cref="Stages"/>);
/// a product with none of these configured behaves like a plain shop item.
/// </summary>
public class StoreProduct : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public List<string>? ImageUrls { get; set; }
    public int QuantityAvailable { get; set; }

    /// <summary>Free-text delivery/pickup instructions set by institution staff — shown to every buyer of this product.</summary>
    public string? DeliveryInfo { get; set; }

    public string Status { get; set; } = "Active"; // Active, Draft, Archived

    /// <summary>Label shown beside the price, e.g. "per night", "per person". Null shows the bare price.</summary>
    public string? PriceLabel { get; set; }

    /// <summary>False means unlimited availability (services, bookings): no stock is checked or decremented and no "left in stock" is shown. True (the default) keeps the original stock behaviour.</summary>
    public bool TrackStock { get; set; } = true;

    /// <summary>Extra "label: value" lines shown on the product page.</summary>
    public List<StoreDetailItem> Details { get; set; } = [];

    /// <summary>What the buyer is asked about this item when ordering (reuses the Services field definitions).</summary>
    public List<ServiceFieldDefinition> Fields { get; set; } = [];

    /// <summary>The delivery or pickup details collected for this item (e.g. address, phone, preferred time).</summary>
    public List<ServiceFieldDefinition> DeliveryFields { get; set; } = [];

    /// <summary>
    /// Ordered fulfilment stages for this product. Empty (the default) keeps the original behaviour:
    /// the order uses the institution-wide delivery stages set in store settings. When set, each
    /// ordered item of this product moves through these stages on its own.
    /// </summary>
    public List<string> Stages { get; set; } = [];

    /// <summary>
    /// Names of the variant option axes this product is sold by, e.g.
    /// ["Size","Color"]. Empty (the default) means this is a simple product
    /// with no <see cref="StoreProductVariant"/> rows — every existing
    /// product keeps working with zero behavior change.
    /// When non-empty, <see cref="Price"/> and <see cref="QuantityAvailable"/>
    /// stop being independently editable roll-ups: Price is recomputed as
    /// the lowest effective variant price ("from" price) and
    /// QuantityAvailable as the sum of all variant quantities, both
    /// recalculated by the service whenever variants change.
    /// </summary>
    public List<string> VariantOptionTypes { get; set; } = [];
}

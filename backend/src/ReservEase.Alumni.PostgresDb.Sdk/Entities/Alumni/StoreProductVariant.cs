using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One purchasable variant of a <see cref="StoreProduct"/> (e.g. a specific
/// Size/Color combination) — its own table (not embedded jsonb on the
/// parent) because checkout needs an atomic per-variant stock decrement.
/// A product with no variants (the common case) has none of these rows;
/// <see cref="StoreProduct.VariantOptionTypes"/> being empty is what tells
/// the rest of the system to treat the product as simple.
/// </summary>
public class StoreProductVariant : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    /// <summary>e.g. {"Size":"Medium","Color":"Red"} — keys match the parent product's VariantOptionTypes.</summary>
    public Dictionary<string, string> Options { get; set; } = new();

    public string? Sku { get; set; }

    /// <summary>Null falls back to the parent StoreProduct.Price for this variant's effective price.</summary>
    public decimal? PriceOverride { get; set; }

    public int QuantityAvailable { get; set; }

    /// <summary>Null falls back to the parent product's first image.</summary>
    public string? ImageUrl { get; set; }
}

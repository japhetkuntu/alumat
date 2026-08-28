using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A physical/merch item an institution's SuperAdmins list for sale.
/// Delivery is handled operationally by institution staff outside the
/// platform — <see cref="DeliveryInfo"/> is just the instructions shown to
/// buyers (e.g. pickup location/hours, shipping timeframe), never an actual
/// fulfillment workflow.
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
}

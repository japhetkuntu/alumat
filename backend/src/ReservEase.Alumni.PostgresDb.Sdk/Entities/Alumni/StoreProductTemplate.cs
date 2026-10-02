using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A reusable store setup (questions, delivery details, stages and display details) an institution
/// saves once and imports into any product. Importing copies the setup into the product, which
/// can then be edited freely; editing a template never changes products that already used it.
/// </summary>
public class StoreProductTemplate : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? PriceLabel { get; set; }
    public bool TrackStock { get; set; } = true;
    public string? DeliveryInfo { get; set; }
    public List<StoreDetailItem> Details { get; set; } = [];
    public List<ServiceFieldDefinition> Fields { get; set; } = [];
    public List<ServiceFieldDefinition> DeliveryFields { get; set; } = [];
    public List<string> Stages { get; set; } = [];
}

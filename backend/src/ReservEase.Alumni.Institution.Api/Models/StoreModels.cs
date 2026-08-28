namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>One variant row submitted with a product create/update — the admin submits the full variant set each time (delete-and-recreate on update, no per-variant diffing).</summary>
public class VariantRequest
{
    /// <summary>JSON-encoded { "Size": "Medium", ... } — sent as a raw string rather than bound as Dictionary&lt;string,string&gt; because ASP.NET Core's form-data dictionary binder lowercases bracket-indexed keys (e.g. "Size" -> "size"), silently breaking case-sensitive option-type matching. Parsed explicitly in StoreService.</summary>
    public string OptionsJson { get; set; } = "{}";
    public string? Sku { get; set; }
    public decimal? PriceOverride { get; set; }
    public int QuantityAvailable { get; set; }
}

public class CreateStoreProductRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int QuantityAvailable { get; set; }
    /// <summary>Delivery/pickup instructions shown to buyers — delivery itself is handled by staff outside the platform.</summary>
    public string? DeliveryInfo { get; set; }
    public string Status { get; set; } = "Active";
    public List<IFormFile>? Images { get; set; }
    /// <summary>Empty (default) means a simple product with no variants — Price/QuantityAvailable above are used as-is.</summary>
    public List<string> VariantOptionTypes { get; set; } = [];
    public List<VariantRequest> Variants { get; set; } = [];
}

public class UpdateStoreProductRequest
{
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int QuantityAvailable { get; set; }
    public string? DeliveryInfo { get; set; }
    public string Status { get; set; } = "Active";
    public List<IFormFile>? Images { get; set; }
    public List<string>? ExistingImageUrls { get; set; }
    /// <summary>Empty (default) means a simple product with no variants — Price/QuantityAvailable above are used as-is.</summary>
    public List<string> VariantOptionTypes { get; set; } = [];
    public List<VariantRequest> Variants { get; set; } = [];
}

public record StoreSettingsResponse(string? DefaultDeliveryInfo, List<string> DeliveryStages);

public class UpdateStoreSettingsRequest
{
    public string? DefaultDeliveryInfo { get; set; }
    public List<string>? DeliveryStages { get; set; }
}

public class UpdateStoreOrderDeliveryStatusRequest
{
    /// <summary>Null clears delivery tracking for the order.</summary>
    public string? DeliveryStatus { get; set; }
}

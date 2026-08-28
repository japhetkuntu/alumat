namespace ReservEase.Alumni.Institution.Api.Models;

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
}

public record StoreSettingsResponse(string? DefaultDeliveryInfo);

public class UpdateStoreSettingsRequest
{
    public string? DefaultDeliveryInfo { get; set; }
}

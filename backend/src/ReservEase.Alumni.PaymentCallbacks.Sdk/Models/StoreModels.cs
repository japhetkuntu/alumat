using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Models;

public class StoreProductFilter : BaseFilter
{
}

public class StoreOrderFilter : BaseFilter
{
    /// <summary>Optional exact-match filter on StoreOrder.DeliveryStatus — used by institution staff to triage orders by stage.</summary>
    public string? DeliveryStatus { get; set; }
}

public class CheckoutItemRequest
{
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    /// <summary>Required when the product has VariantOptionTypes; ignored for simple products.</summary>
    public string? VariantId { get; set; }
    /// <summary>Answers to the product's order questions, keyed by question key. File questions are not sent here (see the multipart checkout).</summary>
    public Dictionary<string, string>? Answers { get; set; }
    /// <summary>Answers to the product's delivery questions, keyed by question key.</summary>
    public Dictionary<string, string>? DeliveryAnswers { get; set; }
}

public class CheckoutRequest
{
    public List<CheckoutItemRequest> Items { get; set; } = [];
    public string? CallbackUrl { get; set; }
}

public class StoreCheckoutResponse
{
    public string? AuthorizationUrl { get; set; }
    public string Reference { get; set; } = "";
}

public class StoreOrderStatusResponse
{
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal? Amount { get; set; }
    public string Message { get; set; } = "";
}

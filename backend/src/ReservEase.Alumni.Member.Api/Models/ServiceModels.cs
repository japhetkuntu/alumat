using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Models;

public class ServiceTypeFilter : BaseFilter
{
}

public class ServiceRequestFilter : BaseFilter
{
    public string? Stage { get; set; }
}

/// <summary>
/// Submitted alongside the member's field answers. File-type field answers
/// aren't bound here — ASP.NET Core's form binder can't cleanly bind a
/// Dictionary&lt;string, IFormFile&gt; (the same bracket-indexed-key
/// lowercasing footgun StoreModels.VariantRequest.OptionsJson works around
/// for Dictionary&lt;string,string&gt;) — instead each file is submitted as
/// its own form part, named exactly as the field's Key, and the controller
/// reads them straight off Request.Form.Files.
/// </summary>
public class CreateServiceRequestRequest
{
    public string ServiceTypeId { get; set; } = "";
    /// <summary>JSON-encoded {"fieldKey":"value", ...} for every non-File field — sent as a raw string since this ships as multipart form data alongside any file attachments.</summary>
    public string AnswersJson { get; set; } = "{}";
    public string? CallbackUrl { get; set; }
}

public class ServiceRequestCheckoutResponse
{
    /// <summary>Null when the service is free — the request is created directly with no payment step.</summary>
    public string? AuthorizationUrl { get; set; }
    public string RequestId { get; set; } = "";
    public string? Reference { get; set; }
}

public class ServiceRequestStatusResponse
{
    public string Reference { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public decimal? Amount { get; set; }
    public string Message { get; set; } = "";
}

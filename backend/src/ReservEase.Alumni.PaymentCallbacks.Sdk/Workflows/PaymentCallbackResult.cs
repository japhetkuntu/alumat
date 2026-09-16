namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// What a payment-callback workflow decided, translated back into an HTTP-friendly
/// shape by the caller (Member.Api's webhook controller ignores this; its
/// verify/status endpoints map it onto their existing IApiResponse contract).
/// Every one of the old monolithic method's distinct return points becomes one of
/// these instead of a direct ApiResponseExtensions.ToXApiResponse call, since the
/// workflow runs in a different process and IApiResponse is an HTTP-layer concept.
/// </summary>
public sealed class PaymentCallbackResult
{
    /// <summary>True for a client-error outcome (ownership mismatch, already-paid
    /// guard, a verify failure) — maps to HTTP 400. False maps to HTTP 200.</summary>
    public bool IsBadRequest { get; init; }
    public string Message { get; init; } = string.Empty;

    public static PaymentCallbackResult Ok(string message) => new() { IsBadRequest = false, Message = message };
    public static PaymentCallbackResult BadRequest(string message) => new() { IsBadRequest = true, Message = message };
}

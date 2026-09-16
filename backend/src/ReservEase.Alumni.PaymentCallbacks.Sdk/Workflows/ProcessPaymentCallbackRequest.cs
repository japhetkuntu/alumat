namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;

/// <summary>
/// Input to <see cref="IProcessPaymentCallbackWorkflow"/>. Deliberately provider-agnostic —
/// <see cref="Provider"/> identifies which gateway sent the callback (e.g. "Paystack") purely
/// for the workflow ID and logging; the routing logic keyed off <see cref="Reference"/>'s
/// prefix (see PaystackReferencePrefix) is what actually decides which domain service owns it.
/// </summary>
public sealed class ProcessPaymentCallbackRequest
{
    public string Provider { get; }
    public string Reference { get; }
    public string RawBody { get; }

    public ProcessPaymentCallbackRequest(string provider, string reference, string rawBody)
    {
        Provider = provider;
        Reference = reference;
        RawBody = rawBody;
    }
}

using Umat.Alumni.Paystack.Sdk.Models;

namespace Umat.Alumni.Paystack.Sdk.Services;

public interface IPaystackService
{
    Task<InitializePaymentResponse> InitializePaymentAsync(InitializePaymentRequest request);
    Task<VerifyPaymentResponse> VerifyPaymentAsync(string reference);
}

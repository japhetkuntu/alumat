using ReservEase.Alumni.Paystack.Sdk.Models;

namespace ReservEase.Alumni.Paystack.Sdk.Services;

public interface IPaystackService
{
    Task<InitializePaymentResponse> InitializePaymentAsync(InitializePaymentRequest request);
    Task<VerifyPaymentResponse> VerifyPaymentAsync(string reference);
}

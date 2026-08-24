using ReservEase.Alumni.Paystack.Sdk.Models;

namespace ReservEase.Alumni.Paystack.Sdk.Services;

public interface IPaystackService
{
    Task<InitializePaymentResponse> InitializePaymentAsync(InitializePaymentRequest request);
    Task<VerifyPaymentResponse> VerifyPaymentAsync(string reference);

    /// <summary>Create a Paystack subaccount for an institution's settlement banking details.</summary>
    Task<SubaccountResponse> CreateSubaccountAsync(SubaccountRequest request);

    /// <summary>Update an existing subaccount (e.g. new bank details or fee percentage) by its code.</summary>
    Task<SubaccountResponse> UpdateSubaccountAsync(string subaccountCode, SubaccountRequest request);
}

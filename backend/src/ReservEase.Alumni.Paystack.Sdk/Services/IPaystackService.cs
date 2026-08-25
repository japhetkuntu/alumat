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

    /// <summary>
    /// Fetches a subaccount by code to check it still exists on Paystack — a
    /// subaccount stored locally can go stale if someone deletes it directly
    /// in the Paystack dashboard, in which case an update call would fail
    /// (or silently no-op) rather than actually syncing anything.
    /// </summary>
    Task<SubaccountResponse> FetchSubaccountAsync(string subaccountCode);

    /// <summary>Real banks ("ghipss") or mobile money providers ("mobile_money") for GHS — lets the UI offer a picklist instead of free-text bank name/code.</summary>
    Task<ListBanksResponse> ListBanksAsync(string type);

    /// <summary>Resolves an account number + bank code to the real account holder name — Paystack looks this up directly with the bank, free for NG/GH.</summary>
    Task<ResolveAccountResponse> ResolveAccountAsync(string accountNumber, string bankCode);
}

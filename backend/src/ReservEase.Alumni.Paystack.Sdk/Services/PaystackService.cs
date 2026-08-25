using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;

namespace ReservEase.Alumni.Paystack.Sdk.Services;

public class PaystackService(PaystackConfig config, IHttpClientFactory httpClientFactory) : IPaystackService
{
    public async Task<InitializePaymentResponse> InitializePaymentAsync(InitializePaymentRequest request)
    {
        var client = CreateClient();
        if (!string.IsNullOrEmpty(config.CallbackUrl) && string.IsNullOrEmpty(request.CallbackUrl))
            request.CallbackUrl = config.CallbackUrl;

        var body = new StringContent(JsonConvert.SerializeObject(new
        {
            email = request.Email,
            amount = request.Amount,
            reference = request.Reference,
            callback_url = request.CallbackUrl,
            metadata = request.Metadata,
            subaccount = request.Subaccount,
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/transaction/initialize", body);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonConvert.DeserializeObject<InitializePaymentResponse>(content, new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
            }
        });
        return responseData!;
    }

    public async Task<VerifyPaymentResponse> VerifyPaymentAsync(string reference)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"/transaction/verify/{reference}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<VerifyPaymentResponse>(content, new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
            }
        })!;
    }

    public async Task<SubaccountResponse> CreateSubaccountAsync(SubaccountRequest request)
    {
        var client = CreateClient();
        var body = SubaccountBody(request);
        var response = await client.PostAsync("/subaccount", body);
        return await DeserializeSubaccountResponse(response);
    }

    public async Task<SubaccountResponse> UpdateSubaccountAsync(string subaccountCode, SubaccountRequest request)
    {
        var client = CreateClient();
        var body = SubaccountBody(request);
        var response = await client.PutAsync($"/subaccount/{subaccountCode}", body);
        return await DeserializeSubaccountResponse(response);
    }

    public async Task<SubaccountResponse> FetchSubaccountAsync(string subaccountCode)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"/subaccount/{subaccountCode}");
        return await DeserializeSubaccountResponse(response);
    }

    private static StringContent SubaccountBody(SubaccountRequest request) => new(JsonConvert.SerializeObject(new
    {
        business_name = request.BusinessName,
        settlement_bank = request.SettlementBank,
        account_number = request.AccountNumber,
        percentage_charge = request.PercentageCharge,
    }), Encoding.UTF8, "application/json");

    private static async Task<SubaccountResponse> DeserializeSubaccountResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<SubaccountResponse>(content, new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
            }
        })!;
    }

    public async Task<ListBanksResponse> ListBanksAsync(string type)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"/bank?currency=GHS&type={Uri.EscapeDataString(type)}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ListBanksResponse>(content, new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
            }
        }) ?? new ListBanksResponse();
    }

    public async Task<ResolveAccountResponse> ResolveAccountAsync(string accountNumber, string bankCode)
    {
        var client = CreateClient();
        var response = await client.GetAsync(
            $"/bank/resolve?account_number={Uri.EscapeDataString(accountNumber)}&bank_code={Uri.EscapeDataString(bankCode)}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ResolveAccountResponse>(content, new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
            }
        }) ?? new ResolveAccountResponse { Message = "Could not resolve account" };
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("Paystack");
        client.BaseAddress = new Uri(config.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.SecretKey);
        return client;
    }
}

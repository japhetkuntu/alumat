using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Umat.Alumni.Paystack.Sdk.Models;
using Umat.Alumni.Paystack.Sdk.Options;

namespace Umat.Alumni.Paystack.Sdk.Services;

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

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("Paystack");
        client.BaseAddress = new Uri(config.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.SecretKey);
        return client;
    }
}

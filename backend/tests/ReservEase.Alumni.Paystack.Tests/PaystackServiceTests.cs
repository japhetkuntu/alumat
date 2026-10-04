using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Paystack.Tests;

public class PaystackServiceTests
{
    private static PaystackService Create(FakeHttpHandler handler, Action<PaystackConfig>? configure = null)
    {
        var config = new PaystackConfig { SecretKey = "sk_test_1", BaseUrl = "https://paystack.test" };
        configure?.Invoke(config);
        return new PaystackService(config, new FakeHttpClientFactory(handler));
    }

    [Fact]
    public async Task Initialize_posts_snake_case_body_with_split_fields_and_bearer_auth()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK,
            "{\"status\":true,\"message\":\"ok\",\"data\":{\"authorization_url\":\"https://pay/x\",\"access_code\":\"ac\",\"reference\":\"r1\"}}");

        var response = await Create(handler).InitializePaymentAsync(new InitializePaymentRequest
        {
            Email = "a@b.c", Amount = 10500, Reference = "r1", CallbackUrl = "https://app/cb",
            Subaccount = "ACCT_1", TransactionCharge = 500, Bearer = "account",
            Metadata = new() { ["k"] = "v" },
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://paystack.test/transaction/initialize", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("sk_test_1", request.Headers.Authorization.Parameter);
        var body = handler.Bodies[0]!;
        Assert.Contains("\"email\":\"a@b.c\"", body);
        Assert.Contains("\"amount\":10500", body);
        Assert.Contains("\"callback_url\":\"https://app/cb\"", body);
        Assert.Contains("\"subaccount\":\"ACCT_1\"", body);
        Assert.Contains("\"transaction_charge\":500", body);
        Assert.Contains("\"bearer\":\"account\"", body);

        Assert.True(response.Status);
        Assert.Equal("https://pay/x", response.Data!.AuthorizationUrl);
        Assert.Equal("ac", response.Data.AccessCode);
        Assert.Equal("r1", response.Data.Reference);
    }

    [Fact]
    public async Task Initialize_falls_back_to_configured_callback_url_when_request_has_none()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":true,\"data\":{}}");
        await Create(handler, c => c.CallbackUrl = "https://config/cb").InitializePaymentAsync(new InitializePaymentRequest { Email = "a@b.c", Amount = 1 });
        Assert.Contains("\"callback_url\":\"https://config/cb\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task Initialize_keeps_request_callback_url_over_config()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":true,\"data\":{}}");
        await Create(handler, c => c.CallbackUrl = "https://config/cb").InitializePaymentAsync(new InitializePaymentRequest { Email = "a@b.c", Amount = 1, CallbackUrl = "https://req/cb" });
        Assert.Contains("\"callback_url\":\"https://req/cb\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task Verify_gets_reference_and_maps_snake_case_response_including_authorization()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, """
        {"status":true,"message":"Verification successful","data":{"status":"success","reference":"r9","amount":12000,"fees":234,
         "gateway_response":"Successful","paid_at":"2026-10-03T08:00:00Z",
         "authorization":{"authorization_code":"AUTH_x","reusable":true,"channel":"card","last4":"4081","card_type":"visa","bank":"TEST","exp_month":"12","exp_year":"2030"}}}
        """);

        var response = await Create(handler).VerifyPaymentAsync("r9");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://paystack.test/transaction/verify/r9", request.RequestUri!.ToString());
        Assert.Equal("success", response.Data!.Status);
        Assert.Equal(12000, response.Data.Amount);
        Assert.Equal(234, response.Data.Fees);
        Assert.Equal("Successful", response.Data.GatewayResponse);
        Assert.Equal("AUTH_x", response.Data.Authorization!.AuthorizationCode);
        Assert.True(response.Data.Authorization.Reusable);
        Assert.Equal("4081", response.Data.Authorization.Last4);
        Assert.Equal("12", response.Data.Authorization.ExpMonth);
    }

    [Fact]
    public async Task Verify_leaves_fees_and_authorization_null_when_absent()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":true,\"data\":{\"status\":\"failed\",\"reference\":\"r\",\"amount\":1}}");
        var response = await Create(handler).VerifyPaymentAsync("r");
        Assert.Null(response.Data!.Fees);
        Assert.Null(response.Data.Authorization);
        Assert.Equal("failed", response.Data.Status);
    }

    [Fact]
    public async Task ChargeAuthorization_posts_to_charge_authorization_with_split_fields()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":true,\"data\":{\"status\":\"success\",\"reference\":\"rc\",\"amount\":5000}}");

        var response = await Create(handler).ChargeAuthorizationAsync(new ChargeAuthorizationRequest
        {
            AuthorizationCode = "AUTH_1", Email = "a@b.c", Amount = 5000, Reference = "rc",
            Subaccount = "ACCT_1", TransactionCharge = 100, Bearer = "account",
        });

        Assert.Equal("https://paystack.test/transaction/charge_authorization", handler.Requests[0].RequestUri!.ToString());
        var body = handler.Bodies[0]!;
        Assert.Contains("\"authorization_code\":\"AUTH_1\"", body);
        Assert.Contains("\"transaction_charge\":100", body);
        Assert.Equal("success", response.Data!.Status);
    }

    [Fact]
    public async Task CreateSubaccount_posts_business_and_bank_details()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":true,\"data\":{\"subaccount_code\":\"ACCT_new\",\"percentage_charge\":0,\"active\":true}}");

        var response = await Create(handler).CreateSubaccountAsync(new SubaccountRequest
        {
            BusinessName = "UMaT", SettlementBank = "044", AccountNumber = "0123456789", PercentageCharge = 0,
        });

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://paystack.test/subaccount", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("\"business_name\":\"UMaT\"", handler.Bodies[0]);
        Assert.Contains("\"settlement_bank\":\"044\"", handler.Bodies[0]);
        Assert.Contains("\"account_number\":\"0123456789\"", handler.Bodies[0]);
        Assert.Equal("ACCT_new", response.Data!.SubaccountCode);
        Assert.True(response.Data.Active);
    }

    [Fact]
    public async Task UpdateSubaccount_puts_to_the_subaccount_code()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":true,\"data\":{\"subaccount_code\":\"ACCT_1\"}}");
        await Create(handler).UpdateSubaccountAsync("ACCT_1", new SubaccountRequest { BusinessName = "X", SettlementBank = "1", AccountNumber = "2" });
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal("https://paystack.test/subaccount/ACCT_1", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchSubaccount_gets_the_subaccount_code_and_reports_not_found_status()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.NotFound, "{\"status\":false,\"message\":\"Subaccount not found\"}");
        var response = await Create(handler).FetchSubaccountAsync("ACCT_gone");
        Assert.Equal("https://paystack.test/subaccount/ACCT_gone", handler.Requests[0].RequestUri!.ToString());
        Assert.False(response.Status);
        Assert.Equal("Subaccount not found", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task ListBanks_requests_ghs_banks_of_the_given_type_and_escapes_it()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK,
            "{\"status\":true,\"data\":[{\"name\":\"MTN\",\"code\":\"MTN\",\"type\":\"mobile_money\",\"currency\":\"GHS\"}]}");

        var response = await Create(handler).ListBanksAsync("mobile money");

        Assert.Equal("https://paystack.test/bank?currency=GHS&type=mobile%20money", handler.Requests[0].RequestUri!.AbsoluteUri);
        var bank = Assert.Single(response.Data);
        Assert.Equal("MTN", bank.Code);
        Assert.Equal("mobile_money", bank.Type);
    }

    [Fact]
    public async Task ListBanks_returns_empty_response_for_a_null_body()
    {
        var response = await Create(FakeHttpHandler.Json(HttpStatusCode.OK, "null")).ListBanksAsync("ghipss");
        Assert.NotNull(response);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task ResolveAccount_escapes_query_values_and_maps_the_account_name()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK,
            "{\"status\":true,\"data\":{\"account_number\":\"0123\",\"account_name\":\"KWAME BOATENG\"}}");

        var response = await Create(handler).ResolveAccountAsync("01 23", "0&4");

        Assert.Equal("https://paystack.test/bank/resolve?account_number=01%2023&bank_code=0%264", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("KWAME BOATENG", response.Data!.AccountName);
    }

    [Fact]
    public async Task ResolveAccount_returns_a_message_for_a_null_body()
    {
        var response = await Create(FakeHttpHandler.Json(HttpStatusCode.OK, "null")).ResolveAccountAsync("1", "2");
        Assert.Equal("Could not resolve account", response.Message);
        Assert.False(response.Status);
    }

    [Fact]
    public void Config_defaults_match_published_ghs_pricing_assumptions()
    {
        var c = new PaystackConfig();
        Assert.Equal("https://api.paystack.co", c.BaseUrl);
        Assert.Equal(1.95m, c.GatewayFeePercentage);
        Assert.Equal(0, c.GatewayFixedFeeSubunit);
        Assert.Null(c.GatewayFeeCapSubunit);
        Assert.Equal(2, c.GatewayFeeSafetyBufferSubunit);
    }

    [Fact]
    public void Model_defaults_generate_unique_references()
    {
        Assert.NotEqual(new InitializePaymentRequest().Reference, new InitializePaymentRequest().Reference);
        Assert.NotEqual(new ChargeAuthorizationRequest().Reference, new ChargeAuthorizationRequest().Reference);
    }

    [Fact]
    public void AddPaystackService_binds_config_as_singleton_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaystackConfig:SecretKey"] = "sk",
            ["PaystackConfig:GatewayFeePercentage"] = "1.5",
            ["PaystackConfig:GatewayFeeCapSubunit"] = "10000",
        }).Build();
        var services = new ServiceCollection();

        services.AddPaystackService(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var config = provider.GetRequiredService<PaystackConfig>();
        Assert.Equal("sk", config.SecretKey);
        Assert.Equal(1.5m, config.GatewayFeePercentage);
        Assert.Equal(10000, config.GatewayFeeCapSubunit);
        Assert.IsType<PaystackService>(scope.ServiceProvider.GetRequiredService<IPaystackService>());
    }
}

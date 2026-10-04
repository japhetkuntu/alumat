using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Common.Tests;

public class GoogleTokenVerifierTests
{
    private static (GoogleTokenVerifier verifier, CapturingLogger<GoogleTokenVerifier> logger) Create(string clientId)
    {
        var logger = new CapturingLogger<GoogleTokenVerifier>();
        return (new GoogleTokenVerifier(Options.Create(new GoogleAuthConfig { ClientId = clientId }), logger), logger);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unconfigured_client_id_rejects_everything_and_logs_an_error(string clientId)
    {
        var (verifier, logger) = Create(clientId);

        Assert.Null(await verifier.VerifyAsync("any.token.here"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("ClientId"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    public async Task Malformed_tokens_are_rejected_with_a_warning_not_an_exception(string token)
    {
        var (verifier, logger) = Create("client-id.apps.googleusercontent.com");

        Assert.Null(await verifier.VerifyAsync(token));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void AddGoogleAuth_binds_the_section_and_registers_the_verifier()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuthConfig:ClientId"] = "abc.apps.googleusercontent.com",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddGoogleAuth(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal("abc.apps.googleusercontent.com", scope.ServiceProvider.GetRequiredService<IOptions<GoogleAuthConfig>>().Value.ClientId);
        Assert.IsType<GoogleTokenVerifier>(scope.ServiceProvider.GetRequiredService<IGoogleTokenVerifier>());
    }

    [Fact]
    public void GoogleIdentity_is_a_value_record()
        => Assert.Equal(new GoogleIdentity("a@x.com", "A", "B", null), new GoogleIdentity("a@x.com", "A", "B", null));
}

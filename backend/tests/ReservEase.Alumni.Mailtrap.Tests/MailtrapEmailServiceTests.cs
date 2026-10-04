using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Extensions;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Mailtrap.Tests;

public class MailtrapEmailServiceTests : IDisposable
{
    private readonly string templateDir = Path.Combine(Path.GetTempPath(), "mailtrap-tests-" + Guid.NewGuid().ToString("N"));

    public MailtrapEmailServiceTests() => Directory.CreateDirectory(templateDir);
    public void Dispose() { if (Directory.Exists(templateDir)) Directory.Delete(templateDir, true); }

    private (MailtrapEmailService service, FakeHttpHandler handler, CapturingLogger<MailtrapEmailService> logger) Create(
        FakeHttpHandler? handler = null, Action<MailtrapConfig>? configure = null)
    {
        handler ??= FakeHttpHandler.Json(HttpStatusCode.OK, "{\"ids\":[\"1\"]}");
        var config = new MailtrapConfig
        {
            ApiKey = "mt-key", BaseUrl = "https://mail.test", TemplateDirectory = templateDir,
            DefaultMessageSource = new MailtrapSender { Name = "UMaT Alumni", Email = "noreply@umat.test" },
        };
        configure?.Invoke(config);
        var logger = new CapturingLogger<MailtrapEmailService>();
        return (new MailtrapEmailService(Options.Create(config), new FakeHttpClientFactory(handler), logger), handler, logger);
    }

    private void WriteTemplate(string id, string html) => File.WriteAllText(Path.Combine(templateDir, id + ".html"), html);

    private static SendEmailRequest Request(string template, object? vars = null, params (string email, string name)[] to) => new()
    {
        TemplateId = template,
        TemplateVariables = vars ?? new { },
        To = (to.Length == 0 ? new[] { (email: "a@x.com", name: "Ama") } : to).Select(t => new EmailContact { Email = t.email, Name = t.name }).ToList(),
    };

    private static JsonElement Payload(FakeHttpHandler handler) => JsonDocument.Parse(handler.Bodies[0]!).RootElement;

    [Fact]
    public async Task Posts_to_the_send_endpoint_with_bearer_auth_and_the_named_client()
    {
        WriteTemplate("welcome", "<p>hi</p>");
        var (service, handler, _) = Create();

        var result = await service.SendEmailAsync(Request("welcome"));

        Assert.True(result.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://mail.test/api/send", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("mt-key", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Payload_has_sender_recipients_subject_html_and_template_category()
    {
        WriteTemplate("welcome", "<p>hi</p>");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("welcome", to: new[] { ("a@x.com", "Ama"), ("b@x.com", "Kofi") }));

        var p = Payload(handler);
        Assert.Equal("noreply@umat.test", p.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("UMaT Alumni", p.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal(2, p.GetProperty("to").GetArrayLength());
        Assert.Equal("Kofi", p.GetProperty("to")[1].GetProperty("name").GetString());
        Assert.Equal("welcome", p.GetProperty("category").GetString());
        Assert.Equal("<p>hi</p>", p.GetProperty("html").GetString());
    }

    [Fact]
    public async Task A_recipient_without_a_name_is_addressed_by_email()
    {
        WriteTemplate("welcome", "x");
        var (service, handler, _) = Create();
        await service.SendEmailAsync(Request("welcome", to: new[] { ("a@x.com", "  ") }));
        Assert.Equal("a@x.com", Payload(handler).GetProperty("to")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Response_ids_are_deserialised_on_success()
    {
        WriteTemplate("welcome", "x");
        var (service, _, _) = Create();
        var result = await service.SendEmailAsync(Request("welcome"));
        Assert.Equal(new[] { "1" }, result.Data!.Ids);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Non_success_status_returns_failure_with_the_body_and_logs_an_error()
    {
        WriteTemplate("welcome", "x");
        var (service, _, logger) = Create(FakeHttpHandler.Json(HttpStatusCode.Unauthorized, "{\"errors\":[\"Unauthorized\"]}"));

        var result = await service.SendEmailAsync(Request("welcome"));

        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.Error);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("a@x.com"));
    }

    [Fact]
    public async Task A_network_exception_is_returned_as_failure_not_thrown()
    {
        WriteTemplate("welcome", "x");
        var (service, _, logger) = Create(FakeHttpHandler.Throwing(new HttpRequestException("down")));

        var result = await service.SendEmailAsync(Request("welcome"));

        Assert.False(result.Success);
        Assert.Equal("down", result.Error);
        Assert.Contains(logger.Entries, e => e.Exception is HttpRequestException);
    }

    [Fact]
    public async Task Template_variables_are_substituted_case_insensitively_and_html_escaped()
    {
        WriteTemplate("hello", "Hi {{first_name}} / {{FIRST_NAME}} / {{note}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("hello", new { first_name = "Ama", note = "<script>alert(\"x\")</script> & co" }));

        var html = Payload(handler).GetProperty("html").GetString();
        Assert.Equal("Hi Ama / Ama / &lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt; &amp; co", html);
    }

    [Fact]
    public async Task Json_element_variables_from_temporal_are_supported()
    {
        WriteTemplate("hello", "{{first_name}}|{{count}}|{{missing}}");
        var (service, handler, _) = Create();
        var vars = JsonSerializer.Deserialize<JsonElement>("{\"first_name\":\"Ama\",\"count\":3,\"missing\":null}");

        var result = await service.SendEmailAsync(Request("hello", vars));

        Assert.True(result.Success);
        Assert.Equal("Ama|3|{{missing}}", Payload(handler).GetProperty("html").GetString());
    }

    [Fact]
    public async Task Brand_variables_default_to_the_sender_name_and_platform_blue()
    {
        WriteTemplate("brand", "{{brand_name}}|{{brand_initial}}|{{brand_color}}|{{brand_text_on_color}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("brand"));

        Assert.Equal("UMaT Alumni|U|#2563eb|#ffffff", Payload(handler).GetProperty("html").GetString());
    }

    [Fact]
    public async Task Brand_name_falls_back_to_member_portal_when_no_sender_name_is_configured()
    {
        WriteTemplate("brand", "{{brand_name}}|{{brand_initial}}");
        var (service, handler, _) = Create(configure: c => c.DefaultMessageSource.Name = "");

        await service.SendEmailAsync(Request("brand"));

        Assert.Equal("Member Portal|M", Payload(handler).GetProperty("html").GetString());
    }

    [Fact]
    public async Task A_per_send_brand_name_and_color_override_the_defaults()
    {
        WriteTemplate("brand", "{{brand_name}}|{{brand_initial}}|{{brand_color}}|{{brand_text_on_color}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("brand", new { brand_name = "  tarkwa Alumni", brand_color = "#FDE047" }));

        Assert.Equal("  tarkwa Alumni|T|#fde047|#111827", Payload(handler).GetProperty("html").GetString());
    }

    [Fact]
    public async Task Accent_family_equals_primary_shades_without_a_distinct_secondary_color()
    {
        WriteTemplate("accent", "{{brand_color}}|{{brand_accent_color}}|{{brand_accent_dark}}|{{brand_color_dark}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("accent", new { brand_color = "#2563eb", brand_secondary_color = "#3b82f6" }));

        var parts = Payload(handler).GetProperty("html").GetString()!.Split('|');
        Assert.Equal(parts[0], parts[1]);
        Assert.Equal(parts[3], parts[2]);
    }

    [Fact]
    public async Task A_distinct_secondary_color_drives_the_accent_family()
    {
        WriteTemplate("accent", "{{brand_color}}|{{brand_accent_color}}|{{brand_text_on_accent}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("accent", new { brand_color = "#2563eb", brand_secondary_color = "#dc2626" }));

        var parts = Payload(handler).GetProperty("html").GetString()!.Split('|');
        Assert.Equal("#2563eb", parts[0]);
        Assert.Equal("#dc2626", parts[1]);
    }

    [Fact]
    public async Task Brand_mark_uses_the_logo_when_present_and_escapes_it()
    {
        WriteTemplate("mark", "{{brand_mark_html}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("mark", new { brand_logo = "https://cdn/x.png?a=1&b=\"2\"", brand_name = "A<B" }));

        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.StartsWith("<img src=\"https://cdn/x.png?a=1&amp;b=&quot;2&quot;\"", html);
        Assert.Contains("alt=\"A&lt;B\"", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public async Task Brand_mark_without_a_logo_is_the_coloured_initial_block_rendered_unescaped()
    {
        WriteTemplate("mark", "{{brand_mark_html}}");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("mark", new { brand_name = "Zeta" }));

        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.StartsWith("<div style=\"width:40px", html);
        Assert.EndsWith(">Z</div>", html);
    }

    [Fact]
    public async Task Image_block_is_empty_without_an_image_url_and_an_escaped_img_with_one()
    {
        WriteTemplate("img", "[{{image_block_html}}]");
        var (service, handler, _) = Create();

        await service.SendEmailAsync(Request("img"));
        Assert.Equal("[]", Payload(handler).GetProperty("html").GetString());

        handler.Bodies.Clear();
        await service.SendEmailAsync(Request("img", new { image_url = "https://img/x.png\" onerror=\"x" }));
        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.Contains("<img src=\"https://img/x.png&quot; onerror=&quot;x\"", html);
    }

    [Theory]
    [InlineData("email-verification", "Your Verification Code — UMaT Alumni")]
    [InlineData("otp", "Your Verification Code — UMaT Alumni")]
    [InlineData("email-verification-link", "Verify Your Email — UMaT Alumni")]
    [InlineData("reset-password", "Password Reset — UMaT Alumni")]
    [InlineData("registration", "Welcome to UMaT Alumni")]
    [InlineData("admin-register", "Admin Account Created — UMaT Alumni")]
    [InlineData("contribution-confirmed", "Contribution Confirmed — UMaT Alumni")]
    [InlineData("event-rsvp-confirmed", "RSVP Confirmed — UMaT Alumni")]
    [InlineData("referral-invitation", "You've Been Invited to UMaT Alumni")]
    [InlineData("digest", "What's new at UMaT Alumni")]
    [InlineData("institution-welcome", "Welcome to UMaT Alumni: set up your account")]
    [InlineData("member-welcome", "Welcome to UMaT Alumni: set up your account")]
    [InlineData("notification", "New Notification — UMaT Alumni")]
    [InlineData("class_note-alert", "UMaT Alumni — Class Note Alert")]
    public async Task Subject_is_derived_from_the_template_id(string template, string expected)
    {
        WriteTemplate(template, "x");
        var (service, handler, _) = Create();
        await service.SendEmailAsync(Request(template));
        Assert.Equal(expected, Payload(handler).GetProperty("subject").GetString());
    }

    [Fact]
    public async Task A_configured_subject_beats_the_generated_one()
    {
        WriteTemplate("registration", "x");
        var (service, handler, _) = Create(configure: c => c.TemplateSubjects["registration"] = "Custom subject");
        await service.SendEmailAsync(Request("registration"));
        Assert.Equal("Custom subject", Payload(handler).GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Missing_template_falls_back_to_a_generic_layout_with_a_warning()
    {
        var (service, handler, logger) = Create();

        var result = await service.SendEmailAsync(Request("no-such-template", new { first_name = "Ama", otp_code = "123456", custom_field = "Value & more" }));

        Assert.True(result.Success);
        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.Contains("Hi <strong>Ama</strong>", html);
        Assert.Contains(">123456</div>", html);
        Assert.Contains("Custom Field", html);
        Assert.Contains("Value &amp; more", html);
        Assert.Contains("This template was missing on disk", html);
        Assert.Contains("<h1>No Such Template</h1>", html);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("no-such-template"));
    }

    [Theory]
    [InlineData("register_url", "https://app/register", "Join UMaT Alumni")]
    [InlineData("verify_url", "https://app/verify", "Verify Email")]
    [InlineData("reset_url", "https://app/reset", "Reset Password")]
    [InlineData("action_url", "https://app/go", "Go to Portal")]
    public async Task Fallback_layout_renders_the_matching_call_to_action(string key, string url, string label)
    {
        var (service, handler, _) = Create();
        var vars = new Dictionary<string, string> { [key] = url };
        var result = await service.SendEmailAsync(Request("nothing", new JsonObjectBag(vars).ToElement()));
        Assert.True(result.Success);
        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.Contains($"href=\"{url}\"", html);
        Assert.Contains($">{label}</a>", html);
    }

    [Fact]
    public async Task Fallback_action_label_is_used_when_provided()
    {
        var (service, handler, _) = Create();
        await service.SendEmailAsync(Request("nothing", new { action_url = "https://x", action_label = "Open it" }));
        Assert.Contains(">Open it</a>", Payload(handler).GetProperty("html").GetString());
    }

    [Fact]
    public async Task Template_directory_is_resolved_against_the_app_base_directory_too()
    {
        var relative = "Templates-" + Guid.NewGuid().ToString("N");
        var dir = Path.Combine(AppContext.BaseDirectory, relative);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "base.html"), "from base dir");
            var (service, handler, _) = Create(configure: c => c.TemplateDirectory = relative);
            await service.SendEmailAsync(Request("base"));
            Assert.Equal("from base dir", Payload(handler).GetProperty("html").GetString());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Config_and_option_defaults()
    {
        var c = new MailtrapConfig();
        Assert.Equal("https://send.api.mailtrap.io", c.BaseUrl);
        Assert.Equal("Templates", c.TemplateDirectory);
        Assert.Empty(c.TemplateSubjects);
        Assert.NotNull(c.DefaultMessageSource);
        Assert.NotNull(c.Templates);
        Assert.Equal(string.Empty, c.Templates.ResetPassword);
    }

    [Fact]
    public void Request_models_default_to_empty_collections()
    {
        var request = new SendEmailRequest();
        Assert.Empty(request.To);
        Assert.Equal(string.Empty, request.TemplateId);
        Assert.NotNull(request.TemplateVariables);
        Assert.Empty(new MailtrapSendMessageResponse().Ids);
    }

    [Fact]
    public void AddMailtrapEmailService_binds_config_and_registers_service()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MailtrapConfig:ApiKey"] = "k",
            ["MailtrapConfig:DefaultMessageSource:Email"] = "from@x.com",
            ["MailtrapConfig:TemplateSubjects:welcome"] = "Hello",
            ["MailtrapConfig:Templates:ResetPassword"] = "reset-password",
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddMailtrapEmailService(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var bound = scope.ServiceProvider.GetRequiredService<IOptions<MailtrapConfig>>().Value;
        Assert.Equal("k", bound.ApiKey);
        Assert.Equal("from@x.com", bound.DefaultMessageSource.Email);
        Assert.Equal("Hello", bound.TemplateSubjects["welcome"]);
        Assert.Equal("reset-password", bound.Templates.ResetPassword);
        Assert.IsType<MailtrapEmailService>(scope.ServiceProvider.GetRequiredService<IEmailService>());
    }

    /// <summary>Builds a JsonElement object from a dictionary — what Temporal hands the service after its serialise/deserialise round trip.</summary>
    private sealed class JsonObjectBag(Dictionary<string, string> values)
    {
        public JsonElement ToElement() => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(values));
    }
}

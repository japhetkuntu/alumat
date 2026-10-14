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
        // A null variable never reaches the inbox as a raw placeholder.
        Assert.Equal("Ama|3|", Payload(handler).GetProperty("html").GetString());
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
        // Every variable any template can insist on, so this stays a test of the subject only.
        await service.SendEmailAsync(Request(template, new
        {
            otp_code = "1", verify_url = "u", reset_url = "u", set_password_url = "u", register_url = "u", referral_code = "c",
            portal_url = "u", title = "t", body = "b",
        }));
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

    // ── Every real template, rendered the way each caller really fills it ────────────────────────────────────────────

    private string RealTemplates => Path.Combine(AppContext.BaseDirectory, "Templates");

    public static IEnumerable<object[]> RealSends() => new[]
    {
        // The smallest set of variables each caller in the codebase supplies. If a caller drops one, this is where it shows.
        new object[] { "email-verification", new { first_name = "Ama", otp_code = "123456" } },
        new object[] { "email-verification-link", new { first_name = "Ama", verify_url = "https://p/verify" } },
        new object[] { "reset-password", new { first_name = "Ama", reset_url = "https://p/reset" } },
        new object[] { "institution-welcome", new { first_name = "Ama", set_password_url = "https://p/set" } },
        new object[] { "member-welcome", new { first_name = "Ama", set_password_url = "https://p/set" } },
        new object[] { "referral-invitation", new { referrer_name = "Kofi", referral_code = "ABC", register_url = "https://p/r" } },
        new object[] { "digest", new { member_first_name = "Ama", portal_url = "https://p", digest_sections_html = "<p>x</p>" } },
        // Notifications are sent from a dozen places; several give no footer reason, no link, or no first name.
        new object[] { "notification", new { title = "Dues reminder", body = "Please pay" } },
        new object[] { "notification", new { first_name = "Ama", title = "T", body = "B", badge_label = "Reminder", action_url = "", action_label = "Open" } },
        new object[] { "notification", new { first_name = (string?)null, title = "T", body = "B", pref_label = (string?)null, action_url = (string?)null } },
    }.Select(r => r);

    [Theory]
    [MemberData(nameof(RealSends))]
    public async Task No_real_template_ever_leaves_a_placeholder_in_the_sent_email(string template, object vars)
    {
        var (service, handler, _) = Create(configure: c => c.TemplateDirectory = RealTemplates);

        var result = await service.SendEmailAsync(Request(template, vars));

        Assert.True(result.Success, result.Error);
        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("}}", html);
    }

    [Fact]
    public async Task A_notification_without_a_link_has_no_button_and_a_truthful_footer()
    {
        var (service, handler, _) = Create(configure: c => c.TemplateDirectory = RealTemplates);

        await service.SendEmailAsync(Request("notification", new { title = "Hello", body = "World", brand_name = "Grace Fellowship" }));

        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.DoesNotContain("class=\"button\"", html);
        Assert.Contains("Hi there,", html);
        Assert.Contains("You&#39;re receiving this because you have an account with Grace Fellowship", html.Replace("You're", "You&#39;re"));
    }

    [Theory]
    [InlineData("reset-password", "reset_url")]
    [InlineData("email-verification", "otp_code")]
    [InlineData("member-welcome", "set_password_url")]
    [InlineData("referral-invitation", "register_url")]
    public async Task A_caller_that_leaves_out_the_key_value_still_gets_its_email_sent_and_the_bug_is_logged(string template, string missing)
    {
        var (service, handler, logger) = Create(configure: c => c.TemplateDirectory = RealTemplates);

        var result = await service.SendEmailAsync(Request(template, new { first_name = "Ama" }));

        // Intended emails are never held back; the message names the gap so the caller gets fixed.
        Assert.True(result.Success, result.Error);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("{{", Payload(handler).GetProperty("html").GetString());
        Assert.Contains(logger.Entries, e => e.Message.Contains(missing));
    }

    // ── The exact variable sets the application's send sites build (kept in step with the code by this table) ──────────

    private static readonly object Brand = new { brand_name = "Grace Fellowship", brand_color = "#7a3e9d", brand_secondary_color = "#e0a526", brand_logo = "https://cdn.test/logo.png" };

    private static Dictionary<string, object?> With(object vars, object? brand = null)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in vars.GetType().GetProperties()) d[p.Name] = p.GetValue(vars);
        foreach (var p in (brand ?? Brand).GetType().GetProperties()) d[p.Name] = p.GetValue(brand ?? Brand);
        return d;
    }

    public static IEnumerable<object[]> AllSendSites() => new (string site, string template, Dictionary<string, object?> vars, string[] mustContain)[]
    {
        ("sign-up code", "email-verification", With(new { first_name = "Ama", otp_code = "482913" }), ["482913", "Ama", "Grace Fellowship"]),
        ("verify email link", "email-verification-link", With(new { first_name = "Ama", verify_url = "https://grace.test/verify?t=1" }), ["https://grace.test/verify?t=1", "Ama"]),
        ("member reset password", "reset-password", With(new { first_name = "Ama", reset_url = "https://grace.test/reset-password?token=x" }), ["reset-password?token=x"]),
        ("staff reset password", "reset-password", With(new { first_name = "Kofi", reset_url = "https://admin.grace.test/reset-password?token=y" }), ["admin.grace.test"]),
        ("platform staff reset", "reset-password", new Dictionary<string, object?> { ["first_name"] = "Esi", ["reset_url"] = "https://platform.test/reset" }, ["platform.test/reset", "Esi"]),
        ("institution welcome", "institution-welcome", With(new { first_name = "Kofi", set_password_url = "https://admin.grace.test/set", institution_portal_url = "https://admin.grace.test", member_portal_url = "https://grace.test" }), ["admin.grace.test/set", "https://grace.test"]),
        ("staff invite", "reset-password", With(new { first_name = "Kofi", reset_url = "https://admin.grace.test/invite" }), ["admin.grace.test/invite"]),
        ("member welcome (import)", "member-welcome", With(new { first_name = "Ama", set_password_url = "https://grace.test/set", member_portal_url = "https://grace.test" }), ["grace.test/set"]),
        ("referral invitation", "referral-invitation", With(new { referrer_name = "Kofi", referral_code = "KOFI42", register_url = "https://grace.test/register?ref=KOFI42" }), ["KOFI42", "Kofi", "register?ref=KOFI42"]),
        ("digest", "digest", With(new { member_first_name = "Ama", digest_period_label = "Weekly", digest_frequency_label = "weekly", digest_sections_html = "<p>Events this week</p>", portal_url = "https://grace.test", preferences_url = "https://grace.test/profile" }), ["Events this week", "grace.test/profile"]),
        ("dues reminder", "notification", With(new { first_name = "Ama", title = "Membership Dues Reminder", body = "Your 2026 dues are due.", badge_label = "Dues Reminder", action_url = "https://grace.test/contributions", action_label = "Pay now", pref_label = "you are a member of Grace Fellowship" }), ["Pay now", "contributions", "you are a member of Grace Fellowship"]),
        ("pledge reminder (scheduled)", "notification", With(new { first_name = "Ama", title = "Pledge due", body = "GHS 50 outstanding.", badge_label = "Pledge Reminder", action_url = "https://grace.test/p", action_label = "Give now", pref_label = "you made a pledge to Grace Fellowship" }), ["Give now", "GHS 50"]),
        ("pledge reminder (admin button)", "notification", With(new { first_name = "Ama", title = "Pledge due", body = "GHS 50 outstanding.", badge_label = "Pledge Reminder", action_url = "https://grace.test/p", action_label = "Give now", pref_label = "you made a pledge to this community" }), ["Give now"]),
        ("event reminder", "notification", With(new { first_name = "Ama", title = "Event tomorrow", body = "Annual gathering at 5pm.", badge_label = "Event Reminder", action_url = "https://grace.test/e", action_label = "See who's going", pref_label = "you are going to an event at Grace Fellowship" }), ["Annual gathering", "going"]),
        ("engagement nudge", "notification", With(new { first_name = "Ama", title = "We miss you", body = "Here is what you missed.", badge_label = "Update", action_url = "https://grace.test", action_label = "Open", pref_label = "you are a member of Grace Fellowship" }), ["We miss you"]),
        ("member status change", "notification", With(new { first_name = "Ama", title = "Your account is active", body = "Welcome aboard.", badge_label = "Account Update", action_url = "https://grace.test", action_label = "Open the portal", pref_label = "your membership status changed" }), ["Welcome aboard"]),
        ("broadcast announcement", "notification", With(new { first_name = "Ama", title = "Service times", body = "We now meet at 9am.", badge_label = "Announcement", pref_label = "you are a member of this community", action_url = "https://grace.test/notifications", action_label = "Open notifications", image_url = "https://cdn.test/b.png" }), ["We now meet at 9am", "cdn.test/b.png"]),
        ("report ready", "notification", With(new { first_name = "Kofi", title = "Your report is ready", body = "Members report.", badge_label = "Reports", action_url = "https://admin.grace.test/reports", action_label = "Download the report", pref_label = "you asked for this report" }), ["Download the report"]),
        ("activation nudge to admins", "notification", With(new { first_name = "Kofi", title = "Finish setting up", body = "3 steps left.", badge_label = "Setup progress", action_url = "https://admin.grace.test", action_label = "Open your portal", pref_label = "you are an administrator of this portal" }), ["3 steps left"]),
        ("platform: activation digest", "notification", new Dictionary<string, object?> { ["first_name"] = "Esi", ["title"] = "Weekly digest", ["body"] = "5 institutions.", ["badge_label"] = "Weekly activation digest", ["action_url"] = "https://platform.test/activation", ["action_label"] = "Open the scorecard", ["pref_label"] = "you are SuperAdmin or Sales staff on the platform" }, ["5 institutions"]),
        ("platform: task reminder", "notification", new Dictionary<string, object?> { ["first_name"] = "Esi", ["title"] = "Task due", ["body"] = "Call Grace.", ["badge_label"] = "Platform task", ["pref_label"] = "you are on the AlumUnion platform team", ["action_url"] = "https://platform.test/t", ["action_label"] = "Open the task" }, ["Call Grace"]),
        ("platform: announcement", "notification", new Dictionary<string, object?> { ["first_name"] = "Kofi", ["title"] = "Maintenance", ["body"] = "Tonight 11pm.", ["badge_label"] = "Notice from AlumUnion", ["pref_label"] = "you are listed as an administrator on AlumUnion", ["action_url"] = "https://admin.grace.test", ["action_label"] = "Open your portal" }, ["Tonight 11pm"]),
    }.Select(x => new object[] { x.site, x.template, x.vars, x.mustContain });

    [Theory]
    [MemberData(nameof(AllSendSites))]
    public async Task Every_send_site_renders_a_complete_email(string site, string template, Dictionary<string, object?> vars, string[] mustContain)
    {
        var (service, handler, logger) = Create(configure: c => c.TemplateDirectory = RealTemplates);

        var result = await service.SendEmailAsync(Request(template, vars));

        Assert.True(result.Success, $"{site}: {result.Error}");
        var html = Payload(handler).GetProperty("html").GetString()!;
        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("}}", html);
        foreach (var expected in mustContain)
            Assert.True(html.Contains(expected) || html.Contains(System.Net.WebUtility.HtmlEncode(expected)), $"{site}: expected the email to contain \"{expected}\"");
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("without") || e.Message.Contains("unresolved"));

        // Kept for eyeballing: set MAIL_PREVIEW_DIR to write each rendered email out as a file.
        var dir = Environment.GetEnvironmentVariable("MAIL_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{site.Replace(' ', '-').Replace(':', '-').Replace('(', '-').Replace(')', '-')}.html"), html);
        }
    }
}

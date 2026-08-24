using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;

namespace ReservEase.Alumni.Mailtrap.Sdk.Services;

/// <summary>
/// Sends transactional email via Mailtrap's Send API. Templates are rendered
/// locally from the .html files in <see cref="MailtrapConfig.TemplateDirectory"/>
/// (the same templates and {{variable}} placeholder mechanism the old Mailhog
/// SMTP sender used) rather than relying on templates hosted in the Mailtrap
/// dashboard — that keeps every template in source control and lets it be
/// edited like any other file, no separate dashboard step required.
/// </summary>
public class MailtrapEmailService(
    IOptions<MailtrapConfig> options,
    IHttpClientFactory httpClientFactory,
    ILogger<MailtrapEmailService> logger) : IEmailService
{
    private readonly MailtrapConfig config = options.Value;

    public async Task<MailtrapResponse<MailtrapSendMessageResponse>> SendEmailAsync(
        SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var variables = ExtractVariables(request.TemplateVariables);
            var html = await RenderHtmlAsync(request.TemplateId, variables);
            var subject = ResolveSubject(request.TemplateId);

            var payload = new
            {
                from = new { email = config.DefaultMessageSource.Email, name = config.DefaultMessageSource.Name },
                to = request.To.Select(t => new
                {
                    email = t.Email,
                    name = string.IsNullOrWhiteSpace(t.Name) ? t.Email : t.Name,
                }),
                subject,
                html,
                category = request.TemplateId,
            };

            var client = httpClientFactory.CreateClient("Mailtrap");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            var body = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{config.BaseUrl}/api/send", body, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Mailtrap send failed ({Status}) to {Recipients} (template: {Template}): {Body}",
                    response.StatusCode, string.Join(", ", request.To.Select(t => t.Email)), request.TemplateId, content);
                return new MailtrapResponse<MailtrapSendMessageResponse> { Success = false, Error = content };
            }

            logger.LogInformation(
                "Email sent via Mailtrap to {Recipients} (template: {Template})",
                string.Join(", ", request.To.Select(t => t.Email)), request.TemplateId);

            var data = JsonConvert.DeserializeObject<MailtrapSendMessageResponse>(content);
            return new MailtrapResponse<MailtrapSendMessageResponse> { Success = true, Data = data };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send email via Mailtrap to {Recipients}",
                string.Join(", ", request.To.Select(t => t.Email)));
            return new MailtrapResponse<MailtrapSendMessageResponse> { Success = false, Error = e.Message };
        }
    }

    private string ResolveSubject(string templateId)
    {
        if (config.TemplateSubjects.TryGetValue(templateId, out var subject))
            return subject;

        var brand = string.IsNullOrWhiteSpace(config.DefaultMessageSource.Name) ? "Alumni Portal" : config.DefaultMessageSource.Name;

        return templateId switch
        {
            "email-verification" or "otp" => $"Your Verification Code — {brand}",
            "email-verification-link" => $"Verify Your Email — {brand}",
            "reset-password" => $"Password Reset — {brand}",
            "registration" => $"Welcome to {brand}",
            "admin-register" => $"Admin Account Created — {brand}",
            "contribution-confirmed" => $"Contribution Confirmed — {brand}",
            "event-rsvp-confirmed" => $"RSVP Confirmed — {brand}",
            "referral-invitation" => $"You've Been Invited to {brand}",
            "notification" => $"New Notification — {brand}",
            _ => $"{brand} — {FormatTemplateId(templateId)}",
        };
    }

    private static string FormatTemplateId(string templateId)
    {
        return string.Join(' ', templateId
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..]));
    }

    private async Task<string> RenderHtmlAsync(string templateId, Dictionary<string, string> variables)
    {
        if (!variables.ContainsKey("brand_name"))
            variables["brand_name"] = string.IsNullOrWhiteSpace(config.DefaultMessageSource.Name) ? "Alumni Portal" : config.DefaultMessageSource.Name;
        variables["brand_initial"] = char.ToUpperInvariant(variables["brand_name"].TrimStart().FirstOrDefault('A')).ToString();

        // Templates use plain hex values everywhere (no CSS custom properties —
        // most mail clients, Outlook especially, don't support them), so the
        // full shade set has to be derived and substituted in here. Callers
        // can override "brand_color" per send (e.g. with an institution's own
        // PrimaryColorHex for tenant-branded email); this is the platform's
        // own default when they don't.
        if (!variables.TryGetValue("brand_color", out var brandColor) || string.IsNullOrWhiteSpace(brandColor))
            brandColor = "#0e7143";
        brandColor = EmailColorPalette.ClampSeed(brandColor);
        variables["brand_color"] = brandColor;
        variables["brand_color_dark"] = EmailColorPalette.Dark(brandColor);
        variables["brand_color_light"] = EmailColorPalette.Light(brandColor);
        variables["brand_color_soft"] = EmailColorPalette.Soft(brandColor);
        variables["brand_text_on_color"] = EmailColorPalette.TextOn(brandColor);

        var templatePath = GetTemplatePath(templateId);
        if (templatePath is not null)
        {
            var templateText = await File.ReadAllTextAsync(templatePath);
            return ReplaceTemplateVariables(templateText, variables);
        }

        logger.LogWarning("No template file found for '{TemplateId}' in {Directory}; using fallback layout", templateId, config.TemplateDirectory);
        return BuildFallbackHtml(templateId, variables);
    }

    private string? GetTemplatePath(string templateId)
    {
        var candidate = Path.Combine(config.TemplateDirectory, $"{templateId}.html");
        if (File.Exists(candidate))
            return candidate;

        var baseCandidate = Path.Combine(AppContext.BaseDirectory, config.TemplateDirectory, $"{templateId}.html");
        if (File.Exists(baseCandidate))
            return baseCandidate;

        return null;
    }

    private static string ReplaceTemplateVariables(string templateText, Dictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            templateText = templateText.Replace($"{{{{{key}}}}}", Sanitize(value), StringComparison.OrdinalIgnoreCase);
        }

        return templateText;
    }

    private static string BuildFallbackHtml(string templateId, Dictionary<string, string> variables)
    {
        var brandColor = variables.GetValueOrDefault("brand_color", "#0e7143");
        var brandColorDark = variables.GetValueOrDefault("brand_color_dark", EmailColorPalette.Dark(brandColor));
        var brandColorLight = variables.GetValueOrDefault("brand_color_light", EmailColorPalette.Light(brandColor));
        var brandColorSoft = variables.GetValueOrDefault("brand_color_soft", EmailColorPalette.Soft(brandColor));

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f4f4f7;color:#333}");
        sb.AppendLine(".wrapper{max-width:600px;margin:0 auto;padding:24px}");
        sb.AppendLine(".card{background:#fff;border-radius:12px;padding:32px;box-shadow:0 1px 3px rgba(0,0,0,.08)}");
        sb.AppendLine(".header{text-align:center;padding-bottom:24px;border-bottom:1px solid #eee;margin-bottom:24px}");
        sb.AppendLine($".header h1{{margin:0;font-size:22px;color:{brandColorDark}}}");
        sb.AppendLine($".header .badge{{display:inline-block;background:{brandColor};color:#fff;font-size:11px;font-weight:700;padding:4px 10px;border-radius:6px;letter-spacing:1px;margin-bottom:8px}}");
        sb.AppendLine(".content{font-size:15px;line-height:1.6}");
        sb.AppendLine($".var-block{{background:{brandColorLight};border:1px solid {brandColorSoft};border-radius:8px;padding:16px 20px;margin:16px 0;font-size:14px}}");
        sb.AppendLine($".var-row{{display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid {brandColorSoft}}}");
        sb.AppendLine(".var-row:last-child{border-bottom:none}");
        sb.AppendLine($".var-label{{font-weight:600;color:{brandColorDark};text-transform:capitalize}}");
        sb.AppendLine(".var-value{color:#333}");
        sb.AppendLine($".otp{{text-align:center;font-size:32px;font-weight:800;letter-spacing:8px;color:{brandColorDark};padding:16px 0}}");
        sb.AppendLine(".footer{text-align:center;font-size:12px;color:#999;padding-top:20px;margin-top:24px;border-top:1px solid #eee}");
        sb.AppendLine($".btn{{display:inline-block;background:{brandColor};color:#fff!important;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:600;font-size:14px;margin:12px 0}}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class=\"wrapper\"><div class=\"card\">");

        var brand = variables.TryGetValue("brand_name", out var b) ? b : "Alumni Portal";
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"<div class=\"badge\">{Sanitize(brand.ToUpperInvariant())}</div>");
        sb.AppendLine($"<h1>{FormatTemplateId(templateId)}</h1>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"content\">");

        if (variables.TryGetValue("first_name", out var firstName))
            sb.AppendLine($"<p>Hi <strong>{Sanitize(firstName)}</strong>,</p>");
        else if (variables.TryGetValue("name", out var name))
            sb.AppendLine($"<p>Hi <strong>{Sanitize(name)}</strong>,</p>");

        if (variables.TryGetValue("otp_code", out var otp))
        {
            sb.AppendLine("<p>Your verification code is:</p>");
            sb.AppendLine($"<div class=\"otp\">{Sanitize(otp)}</div>");
            sb.AppendLine("<p>This code expires in 15 minutes. Do not share it with anyone.</p>");
        }

        if (variables.TryGetValue("register_url", out var registerUrl))
            sb.AppendLine($"<p><a class=\"btn\" href=\"{Sanitize(registerUrl)}\">Join {Sanitize(brand)}</a></p>");
        else if (variables.TryGetValue("verify_url", out var verifyUrl))
            sb.AppendLine($"<p><a class=\"btn\" href=\"{Sanitize(verifyUrl)}\">Verify Email</a></p>");
        else if (variables.TryGetValue("reset_url", out var resetUrl))
            sb.AppendLine($"<p><a class=\"btn\" href=\"{Sanitize(resetUrl)}\">Reset Password</a></p>");
        else if (variables.TryGetValue("action_url", out var actionUrl))
            sb.AppendLine($"<p><a class=\"btn\" href=\"{Sanitize(actionUrl)}\">Go to Portal</a></p>");

        var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "first_name", "name", "otp_code", "register_url", "verify_url", "reset_url", "action_url",
                "brand_name", "brand_initial", "brand_color", "brand_color_dark", "brand_color_light", "brand_color_soft", "brand_text_on_color",
            };

        var remaining = variables.Where(kv => !rendered.Contains(kv.Key)).ToList();
        if (remaining.Count > 0)
        {
            sb.AppendLine("<div class=\"var-block\">");
            foreach (var (key, value) in remaining)
            {
                var label = string.Join(' ', key.Replace('_', ' ').Split(' ')
                    .Select(w => char.ToUpper(w[0]) + w[1..]));
                sb.AppendLine($"<div class=\"var-row\"><span class=\"var-label\">{Sanitize(label)}</span><span class=\"var-value\">{Sanitize(value)}</span></div>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine($"<p>&copy; {Sanitize(brand)}. This template was missing on disk — showing a generic fallback layout.</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div></div></body></html>");

        return sb.ToString();
    }

    private static Dictionary<string, string> ExtractVariables(object templateVariables)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (templateVariables is null) return dict;

        foreach (var prop in templateVariables.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(templateVariables);
            if (value is not null)
                dict[prop.Name] = value.ToString() ?? string.Empty;
        }

        return dict;
    }

    private static string Sanitize(string input)
    {
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}

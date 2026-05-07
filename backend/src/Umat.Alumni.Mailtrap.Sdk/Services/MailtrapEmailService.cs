using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Umat.Alumni.Mailtrap.Sdk.Models;
using Umat.Alumni.Mailtrap.Sdk.Options;

namespace Umat.Alumni.Mailtrap.Sdk.Services;

public class MailtrapEmailService(MailtrapConfig config, IHttpClientFactory httpClientFactory) : IEmailService
{
    public async Task<MailtrapResponse<MailtrapSendMessageResponse>> SendEmailAsync(
        SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Mailtrap");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var payload = new
        {
            from = new { email = config.DefaultMessageSource, name = "UMaT Alumni" },
            to = request.To.Select(t => new { email = t.Email, name = t.Name }),
            template_uuid = request.TemplateId,
            template_variables = request.TemplateVariables,
        };

        var body = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{config.BaseUrl}/api/send", body, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new MailtrapResponse<MailtrapSendMessageResponse> { Success = false, Error = content };

        var data = JsonConvert.DeserializeObject<MailtrapSendMessageResponse>(content);
        return new MailtrapResponse<MailtrapSendMessageResponse> { Success = true, Data = data };
    }
}

namespace Umat.Alumni.Mailtrap.Sdk.Options;

public class MailtrapConfig
{
    public string BaseUrl { get; set; } = "https://send.api.mailtrap.io";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultMessageSource { get; set; } = string.Empty;
    public MailtrapTemplates Templates { get; set; } = new();
}

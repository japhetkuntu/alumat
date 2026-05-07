namespace Umat.Alumni.Mailtrap.Sdk.Models;

public class MailtrapSendMessageResponse
{
    public List<string> Ids { get; set; } = [];
    public string MessageId { get; set; } = string.Empty;
}

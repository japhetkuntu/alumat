namespace Umat.Alumni.Mailtrap.Sdk.Models;

public class SendEmailRequest
{
    public List<EmailContact> To { get; set; } = [];
    public string TemplateId { get; set; } = string.Empty;
    public object TemplateVariables { get; set; } = new();
}

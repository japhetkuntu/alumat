namespace Umat.Alumni.Mailhog.Sdk.Options;

public class MailhogConfig
{
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025;
    public bool UseSsl { get; set; } = false;
    public bool UseStartTls { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string WebUiUrl { get; set; } = "http://localhost:8025";
    public string DefaultSenderName { get; set; } = "UMaT Alumni";
    public string DefaultSenderEmail { get; set; } = "noreply@alumat.umat.edu.gh";
    public string TemplateDirectory { get; set; } = "Templates";
    public Dictionary<string, string> TemplateSubjects { get; set; } = new();
}

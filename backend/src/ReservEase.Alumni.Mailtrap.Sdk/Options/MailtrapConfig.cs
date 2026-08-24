namespace ReservEase.Alumni.Mailtrap.Sdk.Options;

public class MailtrapConfig
{
    public string BaseUrl { get; set; } = "https://send.api.mailtrap.io";
    public string ApiKey { get; set; } = string.Empty;
    public MailtrapSender DefaultMessageSource { get; set; } = new();

    /// <summary>Directory (relative to the app's content root, or absolute) holding the .html templates.</summary>
    public string TemplateDirectory { get; set; } = "Templates";

    /// <summary>Subject line per template id — falls back to a generated subject when a key is missing.</summary>
    public Dictionary<string, string> TemplateSubjects { get; set; } = new();

    /// <summary>
    /// Optional per-purpose override of which template id (filename, without
    /// .html) to render for a given email type — lets callers refer to a
    /// stable purpose name (e.g. "ResetPassword") instead of hardcoding the
    /// underlying template file name.
    /// </summary>
    public MailtrapTemplates Templates { get; set; } = new();
}

public class MailtrapSender
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

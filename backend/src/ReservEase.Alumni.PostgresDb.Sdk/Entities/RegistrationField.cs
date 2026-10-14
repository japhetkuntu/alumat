namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// One extra question a community asks at registration ("Chapter", "Profession", "Church branch"…). Defined by the
/// administrators, never from a fixed list. Answers are kept on the member by <see cref="Id"/>.
/// </summary>
public class RegistrationField
{
    /// <summary>Stable key for the answers. Generated from the first label and never changed afterwards, so renaming a question keeps its answers.</summary>
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>"text" (a short free answer) or "select" (one of <see cref="Options"/>).</summary>
    public string Type { get; set; } = RegistrationFieldTypes.Text;
    public bool Required { get; set; }
    public List<string> Options { get; set; } = [];
}

public static class RegistrationFieldTypes
{
    public const string Text = "text";
    public const string Select = "select";
}

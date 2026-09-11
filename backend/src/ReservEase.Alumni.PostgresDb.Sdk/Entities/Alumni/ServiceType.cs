using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// One field an institution asks a member to fill in when requesting a
/// <see cref="ServiceType"/> — e.g. "Index Number" (Text, required) or
/// "Reason for request" (TextArea, optional). Stored as part of
/// <see cref="ServiceType.Fields"/>; a member's answers are keyed by
/// <see cref="Key"/> in <see cref="ServiceRequest.FieldAnswers"/>.
/// </summary>
public class ServiceFieldDefinition
{
    /// <summary>Stable slug identifying this field — set once at creation, never renamed even if Label changes, since it's the key used in every past ServiceRequest.FieldAnswers.</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Text, TextArea, Number, Date, Select, or File.</summary>
    public string Type { get; set; } = "Text";
    public bool Required { get; set; }
    /// <summary>Choices shown when Type is Select. Ignored otherwise.</summary>
    public List<string>? Options { get; set; }
    public string? HelpText { get; set; }
}

/// <summary>
/// A paid or free service an institution's SuperAdmins offer alumni —
/// academic transcripts, attestation letters, certificate reissue, English
/// proficiency letters, or anything else the institution wants to define.
/// Fully customizable per institution: <see cref="Fields"/> is the form a
/// member fills in to request it, <see cref="Stages"/> is the institution's
/// own fulfillment pipeline (e.g. "Submitted" → "Under Review" →
/// "Ready for Collection"). Same Zero-Deduction platform-fee model as
/// StoreProduct when Price is greater than zero; Price of 0 skips payment
/// entirely.
/// </summary>
public class ServiceType : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    public string Status { get; set; } = "Active"; // Active, Draft, Archived

    public List<ServiceFieldDefinition> Fields { get; set; } = [];

    /// <summary>Ordered pipeline stage names this institution uses for requests of this service type. A new request always starts at Stages[0]. Never empty — defaults to a single "Submitted" stage if the admin doesn't customize it.</summary>
    public List<string> Stages { get; set; } = ["Submitted"];
}

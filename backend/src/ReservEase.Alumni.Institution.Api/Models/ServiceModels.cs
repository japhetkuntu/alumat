namespace ReservEase.Alumni.Institution.Api.Models;

public class ServiceFieldDefinitionRequest
{
    /// <summary>Stable slug — omit/empty on a brand-new field to have the server generate one; always pass back the existing Key when editing a field that already has requests answered against it.</summary>
    public string? Key { get; set; }
    public string Label { get; set; } = "";
    public string Type { get; set; } = "Text"; // Text, TextArea, Number, Date, Select, File
    public bool Required { get; set; }
    public List<string>? Options { get; set; }
    public string? HelpText { get; set; }
}

public class CreateServiceTypeRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public List<ServiceFieldDefinitionRequest> Fields { get; set; } = [];
    public List<string> Stages { get; set; } = ["Submitted"];
}

public class UpdateServiceTypeRequest
{
    public string ServiceTypeId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public List<ServiceFieldDefinitionRequest> Fields { get; set; } = [];
    public List<string> Stages { get; set; } = ["Submitted"];
}

/// <summary>Advance/annotate a request — Stage must be one of the request's ServiceType.Stages when provided. At least one of Stage/Note/Attachment should be set.</summary>
public class UpdateServiceRequestRequest
{
    public string? Stage { get; set; }
    public string? Note { get; set; }
    public IFormFile? Attachment { get; set; }
}

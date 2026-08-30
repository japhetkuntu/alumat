namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>Admin-authored listing — created directly, auto-approved, no owning member.</summary>
public class CreateBusinessListingRequest
{
    public string BusinessName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? ExternalLinkUrl { get; set; }
    public IFormFile? Logo { get; set; }
    public IFormFile? Banner { get; set; }
}

/// <summary>Admin's own direct edit of any listing — bypasses the pending-edit mechanism entirely and overwrites live fields regardless of Status.</summary>
public class UpdateBusinessListingRequest
{
    public string BusinessName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? ExternalLinkUrl { get; set; }
    public IFormFile? Logo { get; set; }
    public IFormFile? Banner { get; set; }
}

public class RejectBusinessListingRequest
{
    public string? AdminNotes { get; set; }
}

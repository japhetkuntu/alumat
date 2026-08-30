namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>Submit a new business listing for admin approval.</summary>
public class SubmitBusinessListingRequest
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

/// <summary>
/// Owner's own edit. If the listing is Approved, these values become a
/// pending edit proposal awaiting admin approval instead of going live
/// immediately. If Pending or Rejected, they overwrite the live fields
/// directly (a Rejected listing also goes back to Pending for re-review).
/// </summary>
public class UpdateMyBusinessListingRequest
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

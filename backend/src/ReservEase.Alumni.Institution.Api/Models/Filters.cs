using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Models;

public class MemberListFilter : BaseFilter
{
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
    public string? JobTitleContains { get; set; }
    public string? LocationContains { get; set; }
}

public class CampaignFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class ContributionInstitutionStaffFilter : BaseFilter
{
    public string? CampaignId { get; set; }
    public string? Status { get; set; }
}

public class JobFilter : BaseFilter
{
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Location { get; set; }
    public DateTime? PostedAfter { get; set; }
    public DateTime? PostedBefore { get; set; }
}

public class EventFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class EventRsvpFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class NewsFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class ForumThreadFilter : BaseFilter
{
    public string? CategoryId { get; set; }
    public string? Filter { get; set; }
}

public class MentorProfileFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class MentorshipRequestFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class ResourceFilter : BaseFilter
{
    public string? Category { get; set; }
    public string? Type { get; set; }
    public DateTime? AddedAfter { get; set; }
    public DateTime? AddedBefore { get; set; }
}

public class StoreProductFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class PhotoAlbumFilter : BaseFilter
{
}

public class AlbumPhotoFilter : BaseFilter
{
}

public class BusinessListingFilter : BaseFilter
{
    /// <summary>Omitted means all statuses — Pending, Approved, Rejected, Blacklisted.</summary>
    public string? Status { get; set; }
}

public class StoreOrderFilter : BaseFilter
{
    public string? Status { get; set; }
    /// <summary>Optional exact-match filter on StoreOrder.DeliveryStatus — lets staff triage orders by fulfillment stage.</summary>
    public string? DeliveryStatus { get; set; }
}

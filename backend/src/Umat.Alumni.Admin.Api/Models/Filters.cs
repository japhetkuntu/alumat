using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Models;

public class MemberListFilter : BaseFilter
{
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
}

public class CampaignFilter : BaseFilter
{
    public string? Status { get; set; }
}

public class ContributionAdminFilter : BaseFilter
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

using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Member.Api.Models;

public class ContributionFilter : BaseFilter
{
    public string? CampaignId { get; set; }
}

public class JobFilter : BaseFilter
{
    public string? Type { get; set; }
    public string? Location { get; set; }
    public DateTime? PostedAfter { get; set; }
    public DateTime? PostedBefore { get; set; }
}

public class NewsFilter : BaseFilter
{
    public string? Category { get; set; }
}

public class ForumThreadFilter : BaseFilter
{
    public string? CategoryId { get; set; }
    public string? Filter { get; set; }
}

public class DirectoryFilter : BaseFilter
{
    public string? DepartmentId { get; set; }
    public int? GraduationYear { get; set; }
}

public class ResourceFilter : BaseFilter
{
    public string? Category { get; set; }
    public string? Type { get; set; }
    public DateTime? AddedAfter { get; set; }
    public DateTime? AddedBefore { get; set; }
}

public class EventFilter : BaseFilter
{
    public string? Status { get; set; }
}

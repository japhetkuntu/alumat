namespace Umat.Alumni.Common.Sdk.Models;

public class BaseFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortColumn { get; set; }
    public string? SortDir { get; set; }
    public string? Search { get; set; }
}

public class PagedResult<T>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public long Count { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int LowerBoundSize { get; set; }
    public int UpperBoundSize { get; set; }
    public IEnumerable<T> Results { get; set; } = [];
}

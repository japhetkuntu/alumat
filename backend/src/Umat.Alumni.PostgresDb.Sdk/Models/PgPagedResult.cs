namespace Umat.Alumni.PostgresDb.Sdk.Models;

public class PgPagedResult<T>
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

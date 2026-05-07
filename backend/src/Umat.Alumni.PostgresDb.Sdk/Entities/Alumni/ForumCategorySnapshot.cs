namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ForumCategorySnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

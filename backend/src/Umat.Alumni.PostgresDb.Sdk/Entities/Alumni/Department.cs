namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ShortCode { get; set; }
}

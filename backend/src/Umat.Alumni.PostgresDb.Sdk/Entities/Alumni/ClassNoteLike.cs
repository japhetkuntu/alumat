namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ClassNoteLike : BaseEntity
{
    public string ClassNoteId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

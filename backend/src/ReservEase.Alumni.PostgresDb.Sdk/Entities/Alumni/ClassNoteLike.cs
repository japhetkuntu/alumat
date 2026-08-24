using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class ClassNoteLike : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string ClassNoteId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

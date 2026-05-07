using System.ComponentModel.DataAnnotations.Schema;

namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class EventRsvp : BaseEntity
{
    public string EventId { get; set; } = string.Empty;

    [NotMapped]
    public EventSnapshot? Event { get; set; }

    public string MemberId { get; set; } = string.Empty;

    [NotMapped]
    public MemberSnapshot? Member { get; set; }

    public string Status { get; set; } = "Confirmed";
}

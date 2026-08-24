using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class EventRsvp : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    [NotMapped]
    public EventSnapshot? Event { get; set; }

    public string MemberId { get; set; } = string.Empty;

    [NotMapped]
    public MemberSnapshot? Member { get; set; }

    public string Status { get; set; } = "Confirmed";
}

using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>A member marking someone else's forum reply as helpful. One per member per reply; nobody is told, so it never adds to anyone's notifications.</summary>
public class ForumPostHelpful : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string PostId { get; set; } = string.Empty;
    /// <summary>The thread the reply is in, kept so a page of replies is counted with one query.</summary>
    public string ThreadId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

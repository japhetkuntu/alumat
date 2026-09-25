namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// The forum categories every institution starts with, so the forum is usable on day one and
/// members always have somewhere to post ("General"). They are ordinary rows: an institution's
/// admins can rename, re-describe, add to or delete them like any category they created themselves.
/// </summary>
public static class ForumCategoryDefaults
{
    private static readonly (string Name, string Description)[] Defaults =
    [
        ("General", "Open conversation about anything on your mind."),
        ("Introductions", "New here? Say hello and tell people a bit about yourself."),
        ("Careers & Jobs", "Career advice, job leads and professional questions."),
        ("Events & Meetups", "Plan and talk about reunions, meetups and gatherings."),
        ("Business & Networking", "Share your business, find partners and make connections."),
        ("Advice & Support", "Ask for help, or share what you have learned."),
    ];

    public static List<ForumCategory> Build(string institutionId, string createdBy = "system") =>
        Defaults.Select((d, i) => new ForumCategory
        {
            InstitutionId = institutionId,
            Name = d.Name,
            Description = d.Description,
            SortOrder = i + 1,
            CreatedBy = createdBy,
        }).ToList();
}

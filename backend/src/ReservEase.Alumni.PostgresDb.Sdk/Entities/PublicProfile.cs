namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// What the institution chooses to show anyone who opens its public landing page: a way to reach it and the people
/// who run it. Nothing here is collected from members. A person appears only because an administrator typed them in.
/// </summary>
public class PublicProfile
{
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public List<PublicLeader> Leaders { get; set; } = [];
}

public class PublicLeader
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    /// <summary>Shown only if the administrator adds it, with that person's agreement.</summary>
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

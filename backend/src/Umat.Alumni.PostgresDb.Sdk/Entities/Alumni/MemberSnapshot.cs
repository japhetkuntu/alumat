namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

public class MemberSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public string? MemberNumber { get; set; }
}

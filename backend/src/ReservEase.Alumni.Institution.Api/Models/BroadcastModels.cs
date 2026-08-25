namespace ReservEase.Alumni.Institution.Api.Models;

public class BroadcastFilter
{
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public int? GraduationYearFrom { get; set; }
    public int? GraduationYearTo { get; set; }
}

public class SendBroadcastRequest
{
    public string? Title { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Channels { get; set; } = new();
    public BroadcastFilter Filter { get; set; } = new();
}

public record BroadcastRecipient(string Id, string Email, string FirstName, string? Phone);

public record BroadcastResult(int RecipientCount, List<string> Channels);

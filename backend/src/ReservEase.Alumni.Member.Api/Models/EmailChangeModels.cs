namespace ReservEase.Alumni.Member.Api.Models;

public record StartEmailChangeRequest(string NewEmail);

public record EmailChangeCodeRequest(string Code);

public record UndoEmailChangeRequest(string Token);

/// <summary>Where a pending change stands, so the page can show the right step after a refresh.</summary>
public record EmailChangeStatusResponse(string Step, string? NewEmail, string CurrentEmailHint, string? NewEmailHint);

/// <summary>The signed-in session for the new address, returned once the change is done.</summary>
public record EmailChangeResultResponse(MemberTokenResponse Session, string Email);

/// <summary>A change in progress, kept in Redis for a few minutes. Never written to the database.</summary>
public class PendingEmailChange
{
    public string NewEmail { get; set; } = string.Empty;
    /// <summary>"Current" while waiting for the code sent to the present address, then "New" for the one sent to the new address.</summary>
    public string Step { get; set; } = "Current";
    public string Otp { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public int Sends { get; set; } = 1;
}

/// <summary>Kept for two days so the old address can put things back.</summary>
public class EmailChangeUndo
{
    public string MemberId { get; set; } = string.Empty;
    public string OldEmail { get; set; } = string.Empty;
    public string NewEmail { get; set; } = string.Empty;
}

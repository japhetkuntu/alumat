namespace ReservEase.Alumni.Sms.Sdk.Services;

public interface ISmsService
{
    /// <summary>Send a single SMS. Returns false (and logs) on any failure rather than throwing — a failed SMS should never break the caller's flow.</summary>
    Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

namespace ReservEase.Alumni.Whatsapp.Sdk.Services;

public interface IWhatsAppService
{
    /// <summary>Send a single WhatsApp message. Returns false (and logs) on any failure rather than throwing — a failed message should never break the caller's flow.</summary>
    Task<bool> SendMessageAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

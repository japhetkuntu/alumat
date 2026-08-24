using ReservEase.Alumni.Mailtrap.Sdk.Models;

namespace ReservEase.Alumni.Mailtrap.Sdk.Services;

public interface IEmailService
{
    Task<MailtrapResponse<MailtrapSendMessageResponse>> SendEmailAsync(
        SendEmailRequest request, CancellationToken cancellationToken = default);
}

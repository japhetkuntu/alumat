using Umat.Alumni.Mailtrap.Sdk.Models;

namespace Umat.Alumni.Mailtrap.Sdk.Services;

public interface IEmailService
{
    Task<MailtrapResponse<MailtrapSendMessageResponse>> SendEmailAsync(
        SendEmailRequest request, CancellationToken cancellationToken = default);
}

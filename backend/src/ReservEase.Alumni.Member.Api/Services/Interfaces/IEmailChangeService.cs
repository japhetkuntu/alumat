using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>
/// Changing the address a member signs in with. Two codes: one to the address they have now (proves it is the owner, not someone
/// at an unlocked screen) and one to the new address (proves it is reachable). Nothing changes until both are right.
/// </summary>
public interface IEmailChangeService
{
    Task<IApiResponse<EmailChangeStatusResponse>> GetStatusAsync(AuthData auth);
    Task<IApiResponse<EmailChangeStatusResponse>> StartAsync(StartEmailChangeRequest request, AuthData auth);
    Task<IApiResponse<EmailChangeStatusResponse>> ResendAsync(AuthData auth);
    Task<IApiResponse<EmailChangeStatusResponse>> ConfirmCurrentAsync(EmailChangeCodeRequest request, AuthData auth);
    Task<IApiResponse<EmailChangeResultResponse>> ConfirmNewAsync(EmailChangeCodeRequest request, AuthData auth);
    Task<IApiResponse<object>> CancelAsync(AuthData auth);

    /// <summary>Puts the old address back, from the link sent to it. No sign-in needed: the link is the proof.</summary>
    Task<IApiResponse<object>> UndoAsync(UndoEmailChangeRequest request);
}

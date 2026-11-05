using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberIntroductionService
{
    Task<IApiResponse<IntroductionStateDto>> GetStateAsync(string memberId, AuthData me);
    Task<IApiResponse<IntroductionStateDto>> RequestAsync(RequestIntroductionRequest request, AuthData me);
    /// <param name="box">"chats" (open conversations), "archived" (ended or blocked), "incoming" (asked of me, waiting) or "sent" (asked by me, no answer yet).</param>
    /// <param name="cursor">From the previous page's NextCursor; null for the first page.</param>
    Task<IApiResponse<IntroductionPageDto>> ListAsync(string box, string? cursor, int limit, string? search, AuthData me);
    Task<IApiResponse<IntroductionSummaryDto>> GetSummaryAsync(AuthData me);
    /// <summary>Opens a conversation. With neither <paramref name="after"/> nor <paramref name="before"/>: the newest page. <paramref name="before"/>: the page of older messages. <paramref name="after"/>: whatever is newer than that.</summary>
    Task<IApiResponse<IntroductionDetailDto>> GetAsync(string id, DateTime? after, AuthData me, DateTime? before = null, int? limit = null);
    Task<IApiResponse<object>> RespondAsync(string id, bool accept, AuthData me);
    Task<IApiResponse<object>> WithdrawAsync(string id, AuthData me);
    Task<IApiResponse<object>> EndAsync(string id, AuthData me);
    Task<IApiResponse<object>> ResumeAsync(string id, AuthData me);
    Task<IApiResponse<IntroductionMessageDto>> SendMessageAsync(string id, string body, AuthData me);
    Task<IApiResponse<object>> ReportAsync(string id, ReportIntroductionRequest request, AuthData me);
    Task<IApiResponse<object>> BlockAsync(string memberId, AuthData me);
    Task<IApiResponse<object>> UnblockAsync(string memberId, AuthData me);
    Task<IApiResponse<List<IntroductionPersonDto>>> ListBlockedAsync(AuthData me);
}

namespace ReservEase.Alumni.Member.Api.Models;

public record RequestIntroductionRequest(string MemberId, string Reason);
public record SendIntroductionMessageRequest(string Body);
public record ReportIntroductionRequest(string Reason, string? Details);
public record BlockMemberRequest(string MemberId);

/// <summary>Just enough about another member to show them: never an email or phone.</summary>
/// <summary>Headline is the job title and company, and only when that member shows their company on the directory.</summary>
public record IntroductionPersonDto(string Id, string Name, string? PhotoUrl, int? GraduationYear, string? Headline = null);

/// <summary>Where this member stands with one other member: "self", "none" (can ask), "pending" (asked, no reply yet), "incoming" (they asked me), "connected", "resumable" (I ended a conversation and can reopen it), "reconnectable" (they ended one; I can ask them to reopen it), "blocked" (I blocked them) or "unavailable" (they cannot be asked, for any reason: it never says which).</summary>
public record IntroductionStateDto(string State, string? IntroductionId, DateTime? AskedAt = null);

public record IntroductionItemDto(
    string Id, string Status, bool Mine, IntroductionPersonDto Other, string? Reason, DateTime CreatedAt,
    string? LastMessagePreview, DateTime? LastMessageAt, bool Unread, bool LastMessageMine = false,
    string Condition = "open", bool EndedByMe = false,
    /// <summary>For a request: when the two already had a conversation that was ended, its id. Accepting then reopens that conversation instead of starting a second one.</summary>
    string? ReopensIntroductionId = null);

/// <summary>
/// How a conversation stands for the member looking at it. "open" (can chat), "ended" (closed by one of them, can be resumed by whoever ended it),
/// "blocked" (this member blocked the other) or "unavailable" (messages cannot be sent for a reason that is not shown, which includes being blocked).
/// </summary>
public static class IntroductionConditions { public const string Open = "open", Ended = "ended", Blocked = "blocked", Unavailable = "unavailable"; }

/// <summary>Where to go after answering a request: the conversation it opened, or reopened.</summary>
public record IntroductionTargetDto(string IntroductionId);

public record IntroductionMessageDto(string Id, bool Mine, string Body, DateTime CreatedAt);

/// <summary>OtherReadAt is when the other member last opened the conversation, so the chat can say "Seen".</summary>
/// <summary>HasMoreBefore: older messages exist than the ones returned (scroll up to load them). HasMoreAfter: more new messages exist than fit in this reply (ask again).</summary>
public record IntroductionDetailDto(IntroductionItemDto Item, bool CanSend, List<IntroductionMessageDto> Messages, DateTime? OtherReadAt = null, bool HasMoreBefore = false, bool HasMoreAfter = false);

/// <summary>ArchivedChats is how many ended or blocked conversations there are, so the list can show "Ended and blocked · 12" before loading any of them.</summary>
public record IntroductionSummaryDto(int UnreadChats, int IncomingRequests, int ArchivedChats = 0);

/// <summary>One page of a list, newest activity first. NextCursor is null on the last page; pass it back to get the page after.</summary>
public record IntroductionPageDto(List<IntroductionItemDto> Items, string? NextCursor);

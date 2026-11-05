using System.Text;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Member.Api.Services.Realtime;
using ReservEase.Alumni.Member.Api.Services.Security;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Members asking each other for an introduction, and the private chat that follows when the other member accepts.
/// The rules that keep it safe live here: nothing is shared before a yes, a block works both ways without telling either side,
/// a decline is never announced, and how many requests and messages one person can send is capped.
/// </summary>
public class MemberIntroductionService(
    IAlumniPgRepository<Introduction> introRepo,
    IAlumniPgRepository<IntroductionMessage> messageRepo,
    IAlumniPgRepository<MemberBlock> blockRepo,
    IAlumniPgRepository<ContentReport> reportRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    IMessageProtector protector,
    IIntroductionEvents events,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    ILogger<MemberIntroductionService> logger) : IMemberIntroductionService
{
    public const int MinReason = 10, MaxReason = 300, MaxMessage = 2000;
    /// <summary>Requests a member can have waiting for an answer at once.</summary>
    public const int MaxPendingSent = 5;
    public const int MaxNewPerDay = 10;
    /// <summary>After a request is declined, how long before the same member can ask again.</summary>
    public const int AskAgainAfterDays = 30;
    public const int MessagesPerMinute = 20;
    private const int DefaultListPage = 25, MaxListPage = 50, DefaultThreadPage = 40, MaxThreadPage = 100, ReportedMessages = 20;

    // A list cursor is the last row's activity time and id: it stays correct however much happens meanwhile, and costs the same on page 1 or page 500.
    private static string EncodeCursor(Introduction i)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{i.LastActivityAt.Ticks}|{i.Id}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeCursor(string? cursor, out DateTime at, out string id)
    {
        at = default; id = string.Empty;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var b64 = cursor.Replace('-', '+').Replace('_', '/');
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=')));
            var cut = text.IndexOf('|');
            if (cut <= 0 || !long.TryParse(text.AsSpan(0, cut), out var ticks) || ticks is < 0 or > 3_155_378_975_999_999_999L) return false;
            at = new DateTime(ticks, DateTimeKind.Utc); id = text[(cut + 1)..];
            return id.Length > 0;
        }
        catch { return false; }
    }

    private const string NotTakingIntroductions = "This member isn't taking introductions right now.";

    private sealed record Person(string Id, string FirstName, string LastName, string? ProfilePictureUrl, int GraduationYear, string Status, bool AllowIntroductions, string? JobTitle, string? Company, bool ShowCompany);

    private async Task<Dictionary<string, Person>> PeopleAsync(IEnumerable<string> ids)
    {
        var list = ids.Distinct().ToList();
        return (await memberRepo.GetQueryable(m => list.Contains(m.Id))
                .Select(m => new Person(m.Id, m.FirstName, m.LastName, m.ProfilePictureUrl, m.GraduationYear, m.Status, m.AllowIntroductions, m.JobTitle, m.Company, m.ShowCompanyOnDirectory))
                .ToListAsync())
            .ToDictionary(p => p.Id);
    }

    // What members write is stored encrypted, and each value is bound to its own row and field so it cannot be copied onto another.
    private const string ReasonField = "reason", PreviewField = "preview";
    private static string MessageContext(string introductionId, string messageId) => $"intro:{introductionId}:message:{messageId}";
    private static string FieldContext(string introductionId, string field) => $"intro:{introductionId}:{field}";
    private const string Unreadable = "[This message can't be shown]";

    private string Read(string? stored, string context)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;
        try { return protector.Unprotect(stored, context); }
        catch (Exception e) { logger.LogError(e, "A stored introduction text could not be decrypted ({Context})", context); return Unreadable; }
    }

    private static string NameOf(Person p) => $"{p.FirstName} {p.LastName}".Trim();
    private static string? HeadlineOf(Person p)
    {
        if (!p.ShowCompany) return null;
        var job = p.JobTitle?.Trim();
        var company = p.Company?.Trim();
        if (string.IsNullOrEmpty(job)) return string.IsNullOrEmpty(company) ? null : company;
        return string.IsNullOrEmpty(company) ? job : $"{job} at {company}";
    }

    private static IntroductionPersonDto Dto(Person p) => new(p.Id, NameOf(p), p.ProfilePictureUrl, p.GraduationYear > 0 ? p.GraduationYear : null, HeadlineOf(p));

    private async Task<bool> IsBlockedAsync(string a, string b)
        => await blockRepo.GetOneAsync(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a)) is not null;

    /// <summary>Whether <paramref name="me"/> blocked the other member, and whether the other member blocked me. Blocking is never shown to the one blocked.</summary>
    private async Task<(bool IBlocked, bool BlockedMe)> BlockStateAsync(string me, string other)
    {
        var rows = (await blockRepo.GetAllAsync(x => (x.BlockerId == me && x.BlockedId == other) || (x.BlockerId == other && x.BlockedId == me))).ToList();
        return (rows.Any(r => r.BlockerId == me), rows.Any(r => r.BlockerId == other));
    }

    private static string ConditionOf(Introduction i, bool iBlocked, bool blockedMe)
        => iBlocked ? IntroductionConditions.Blocked
         : blockedMe ? IntroductionConditions.Unavailable
         : i.Status == IntroductionStatuses.Ended ? IntroductionConditions.Ended
         : IntroductionConditions.Open;

    private const string CannotSendNow = "You can't send messages in this conversation right now.";

    private static bool IsParty(Introduction i, string memberId) => i.RequesterId == memberId || i.RecipientId == memberId;
    private static string OtherId(Introduction i, string memberId) => i.RequesterId == memberId ? i.RecipientId : i.RequesterId;

    /// <summary>What a member sees as the status. A declined request reads as still waiting for the one who asked.</summary>
    private static string VisibleStatus(Introduction i, string memberId)
        => i.Status == IntroductionStatuses.Declined && i.RequesterId == memberId ? IntroductionStatuses.Pending : i.Status;

    private static bool IsUnread(Introduction i, string memberId)
    {
        if (i.LastMessageAt is null || i.LastMessageSenderId == memberId) return false;
        var seen = i.RequesterId == memberId ? i.RequesterReadAt : i.RecipientReadAt;
        return seen is null || i.LastMessageAt > seen;
    }

    /// <summary>The conversation between two members that was ended, if any (the most recent one). A new request between them reopens it rather than starting another.</summary>
    private async Task<Introduction?> EndedBetweenAsync(string a, string b, string? exceptId = null)
        => (await introRepo.GetAllAsync(i => i.Status == IntroductionStatuses.Ended && i.Id != exceptId
                && ((i.RequesterId == a && i.RecipientId == b) || (i.RequesterId == b && i.RecipientId == a))))
            .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt).FirstOrDefault();

    private IntroductionItemDto Item(Introduction i, string memberId, Person other, string condition = IntroductionConditions.Open)
        => new(i.Id, VisibleStatus(i, memberId), i.RequesterId == memberId, Dto(other), Read(i.Reason, FieldContext(i.Id, ReasonField)), i.CreatedAt,
            i.LastMessagePreview is null ? null : Read(i.LastMessagePreview, FieldContext(i.Id, PreviewField)), i.LastMessageAt, IsUnread(i, memberId), i.LastMessageSenderId == memberId,
            condition, i.EndedById == memberId);

    /// <summary>Tells a member's open pages that something changed (no content: the page asks for it).</summary>
    private void Tell(string memberId, string kind, string introductionId)
        => events.Publish(currentTenant.InstitutionId!, memberId, new IntroductionEvent(kind, introductionId));

    private Task Notify(string recipientId, string title, string body, string introductionId, string? key = null)
        => temporalProvider.EnqueueNotificationAsync(NotificationRequest.IntroductionUpdate(currentTenant.InstitutionId!, recipientId, title, body, introductionId, key), logger);

    public async Task<IApiResponse<IntroductionStateDto>> GetStateAsync(string memberId, AuthData me)
    {
        try
        {
            if (memberId == me.Id) return new IntroductionStateDto("self", null).ToOkApiResponse();
            var people = await PeopleAsync([memberId]);
            if (!people.TryGetValue(memberId, out var them) || them.Status != "Active")
                return new IntroductionStateDto("unavailable", null).ToOkApiResponse();
            var (iBlocked, blockedMe) = await BlockStateAsync(me.Id, memberId);
            if (iBlocked) return new IntroductionStateDto("blocked", null).ToOkApiResponse();      // the member can undo this themselves
            if (blockedMe) return new IntroductionStateDto("unavailable", null).ToOkApiResponse(); // never reveals why

            var between = (await introRepo.GetAllAsync(i =>
                    (i.RequesterId == me.Id && i.RecipientId == memberId) || (i.RequesterId == memberId && i.RecipientId == me.Id)))
                .OrderByDescending(i => i.CreatedAt).ToList();

            if (between.FirstOrDefault(i => i.Status == IntroductionStatuses.Accepted) is { } accepted)
                return new IntroductionStateDto("connected", accepted.Id).ToOkApiResponse();
            if (between.FirstOrDefault(i => i.Status == IntroductionStatuses.Pending && i.RecipientId == me.Id) is { } incoming)
                return new IntroductionStateDto("incoming", incoming.Id).ToOkApiResponse();
            var cooling = DateTime.UtcNow.AddDays(-AskAgainAfterDays);
            if (between.FirstOrDefault(i => i.RequesterId == me.Id && (i.Status == IntroductionStatuses.Pending || (i.Status == IntroductionStatuses.Declined && i.CreatedAt > cooling))) is { } mine)
                return new IntroductionStateDto("pending", mine.Id, mine.CreatedAt).ToOkApiResponse();
            // A conversation I ended can be reopened by me. One the other person ended can only be asked for again, like any new introduction.
            if (between.FirstOrDefault(i => i.Status == IntroductionStatuses.Ended && i.EndedById == me.Id) is { } ended)
                return new IntroductionStateDto("resumable", ended.Id).ToOkApiResponse();
            // They ended it: I cannot reopen it myself, but I can ask, and if they accept it is this same conversation that comes back.
            if (between.FirstOrDefault(i => i.Status == IntroductionStatuses.Ended) is { } endedByThem && them.AllowIntroductions)
                return new IntroductionStateDto("reconnectable", endedByThem.Id).ToOkApiResponse();
            if (!them.AllowIntroductions) return new IntroductionStateDto("unavailable", null).ToOkApiResponse();
            return new IntroductionStateDto("none", null).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reading introduction state between {MemberId} and {OtherId}", me.Id, memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntroductionStateDto>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<IntroductionStateDto>> RequestAsync(RequestIntroductionRequest request, AuthData me)
    {
        try
        {
            var reason = (request.Reason ?? string.Empty).Trim();
            if (request.MemberId == me.Id) return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionStateDto>("You can't ask to be introduced to yourself.");
            if (reason.Length < MinReason) return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionStateDto>($"Say a little about why you'd like to connect (at least {MinReason} characters).");
            if (reason.Length > MaxReason) return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionStateDto>($"Please keep your note to {MaxReason} characters.");

            // Not taking introductions, deactivated and blocked all read the same, so nobody learns who has blocked them.
            var people = await PeopleAsync([request.MemberId]);
            if (!people.TryGetValue(request.MemberId, out var them) || them.Status != "Active" || !them.AllowIntroductions || await IsBlockedAsync(me.Id, request.MemberId))
                return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionStateDto>(NotTakingIntroductions);

            var between = (await introRepo.GetAllAsync(i =>
                    (i.RequesterId == me.Id && i.RecipientId == request.MemberId) || (i.RequesterId == request.MemberId && i.RecipientId == me.Id)))
                .OrderByDescending(i => i.CreatedAt).ToList();
            if (between.FirstOrDefault(i => i.Status == IntroductionStatuses.Accepted) is { } accepted)
                return ApiResponseExtensions.ToConflictApiResponse<IntroductionStateDto>("You're already connected.");
            if (between.Any(i => i.Status == IntroductionStatuses.Pending && i.RecipientId == me.Id))
                return ApiResponseExtensions.ToConflictApiResponse<IntroductionStateDto>("They've already asked to be introduced to you. Check your requests.");
            var cooling = DateTime.UtcNow.AddDays(-AskAgainAfterDays);
            if (between.Any(i => i.RequesterId == me.Id && (i.Status == IntroductionStatuses.Pending || (i.Status == IntroductionStatuses.Declined && i.CreatedAt > cooling))))
                return ApiResponseExtensions.ToConflictApiResponse<IntroductionStateDto>("You've already asked. They'll see your note when they next visit.");
            if (between.Any(i => i.Status == IntroductionStatuses.Ended && i.EndedById == me.Id))
                return ApiResponseExtensions.ToConflictApiResponse<IntroductionStateDto>("You ended this conversation, so you can simply resume it.");

            if (await introRepo.CountAsync(i => i.RequesterId == me.Id && i.Status == IntroductionStatuses.Pending) >= MaxPendingSent)
                return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionStateDto>($"You have {MaxPendingSent} requests waiting for an answer. Withdraw one or wait for a reply before asking someone else.");
            var dayAgo = DateTime.UtcNow.AddDays(-1);
            if (await introRepo.CountAsync(i => i.RequesterId == me.Id && i.CreatedAt > dayAgo) >= MaxNewPerDay)
                return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionStateDto>("You've sent a lot of requests today. Please try again tomorrow.");

            var intro = new Introduction { RequesterId = me.Id, RecipientId = request.MemberId, CreatedBy = me.Id };
            intro.Reason = protector.Protect(reason, FieldContext(intro.Id, ReasonField));
            await introRepo.AddAsync(intro);
            Tell(request.MemberId, "request", intro.Id);
            await Notify(request.MemberId, "Someone would like an introduction", $"{me.Name} would like to be introduced to you. Open your requests to read their note.", intro.Id);

            logger.LogInformation("Introduction {IntroductionId} requested by {MemberId}", intro.Id, me.Id);
            return new IntroductionStateDto("pending", intro.Id, intro.CreatedAt).ToCreatedApiResponse("Request sent");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error requesting an introduction to {MemberId} by {Me}", request.MemberId, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntroductionStateDto>("Something went wrong. Please try again.");
        }
    }

    /// <summary>The conversations a member has been blocked from or has blocked, either way, as the other member's id.</summary>
    private async Task<(HashSet<string> IBlocked, HashSet<string> BlockedMe)> BlockSetsAsync(string me)
    {
        var blocks = (await blockRepo.GetAllAsync(b => b.BlockerId == me || b.BlockedId == me)).ToList();
        return (blocks.Where(b => b.BlockerId == me).Select(b => b.BlockedId).ToHashSet(), blocks.Where(b => b.BlockedId == me).Select(b => b.BlockerId).ToHashSet());
    }

    private IQueryable<Introduction>? BoxQuery(string box, string me, List<string> apart) => box switch
    {
        "chats" => introRepo.GetQueryable(i => i.Status == IntroductionStatuses.Accepted && (i.RequesterId == me || i.RecipientId == me)
            && !apart.Contains(i.RequesterId == me ? i.RecipientId : i.RequesterId)),
        "archived" => introRepo.GetQueryable(i => (i.Status == IntroductionStatuses.Accepted || i.Status == IntroductionStatuses.Ended) && (i.RequesterId == me || i.RecipientId == me)
            && (i.Status == IntroductionStatuses.Ended || apart.Contains(i.RequesterId == me ? i.RecipientId : i.RequesterId))),
        "incoming" => introRepo.GetQueryable(i => i.Status == IntroductionStatuses.Pending && i.RecipientId == me && !apart.Contains(i.RequesterId)),
        "sent" => introRepo.GetQueryable(i => (i.Status == IntroductionStatuses.Pending || i.Status == IntroductionStatuses.Declined) && i.RequesterId == me && !apart.Contains(i.RecipientId)),
        _ => null,
    };

    public async Task<IApiResponse<IntroductionPageDto>> ListAsync(string box, string? cursor, int limit, string? search, AuthData me)
    {
        try
        {
            var take = Math.Clamp(limit <= 0 ? DefaultListPage : limit, 1, MaxListPage);
            var (iBlocked, blockedMe) = await BlockSetsAsync(me.Id);
            var apart = iBlocked.Union(blockedMe).ToList();
            var meId = me.Id;
            var query = BoxQuery(box, meId, apart);
            if (query is null) return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionPageDto>("Unknown list.");

            // Searching by name: find the matching members first (a handful), then keep only the conversations with them.
            var needle = search?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(needle))
            {
                var ids = await memberRepo.GetQueryable(m => m.Status == "Active" && (m.FirstName + " " + m.LastName).ToLower().Contains(needle)).Select(m => m.Id).Take(300).ToListAsync();
                query = query.Where(i => ids.Contains(i.RequesterId == meId ? i.RecipientId : i.RequesterId));
            }
            if (TryDecodeCursor(cursor, out var at, out var afterId))
                query = query.Where(i => i.LastActivityAt < at || (i.LastActivityAt == at && i.Id.CompareTo(afterId) < 0));

            var rows = await query.OrderByDescending(i => i.LastActivityAt).ThenByDescending(i => i.Id).Take(take + 1).ToListAsync();
            var more = rows.Count > take;
            if (more) rows.RemoveAt(take);

            var people = await PeopleAsync(rows.Select(i => OtherId(i, meId)));
            // For requests waiting on me: did the two already have a conversation that was ended? Accepting then reopens it.
            var endedWith = new Dictionary<string, string>();
            if (box == "incoming" && rows.Count > 0)
            {
                var others = rows.Select(i => OtherId(i, meId)).ToList();
                endedWith = (await introRepo.GetAllAsync(i => i.Status == IntroductionStatuses.Ended
                        && ((i.RequesterId == meId && others.Contains(i.RecipientId)) || (i.RecipientId == meId && others.Contains(i.RequesterId)))))
                    .GroupBy(i => OtherId(i, meId)).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.LastActivityAt).First().Id);
            }

            // Someone who has left drops out instead of showing a broken row; the cursor still moves past them.
            var items = rows
                .Where(i => people.TryGetValue(OtherId(i, meId), out var o) && o.Status == "Active")
                .Select(i => Item(i, meId, people[OtherId(i, meId)], ConditionOf(i, iBlocked.Contains(OtherId(i, meId)), blockedMe.Contains(OtherId(i, meId))))
                    with { ReopensIntroductionId = endedWith.GetValueOrDefault(OtherId(i, meId)) })
                .ToList();
            return new IntroductionPageDto(items, more ? EncodeCursor(rows[^1]) : null).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing introductions ({Box}) for {MemberId}", box, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntroductionPageDto>("Something went wrong. Please try again.");
        }
    }

    /// <summary>Three COUNT queries, however many conversations a member has: nothing is loaded into memory to be counted.</summary>
    public async Task<IApiResponse<IntroductionSummaryDto>> GetSummaryAsync(AuthData me)
    {
        try
        {
            var meId = me.Id;
            var (iBlocked, blockedMe) = await BlockSetsAsync(meId);
            var apart = iBlocked.Union(blockedMe).ToList();
            var incoming = await BoxQuery("incoming", meId, apart)!.CountAsync();
            var archived = await BoxQuery("archived", meId, apart)!.CountAsync();
            var unread = await BoxQuery("chats", meId, apart)!.CountAsync(i => i.LastMessageAt != null && i.LastMessageSenderId != meId
                && ((i.RequesterId == meId && (i.RequesterReadAt == null || i.LastMessageAt > i.RequesterReadAt))
                    || (i.RecipientId == meId && (i.RecipientReadAt == null || i.LastMessageAt > i.RecipientReadAt))));
            return new IntroductionSummaryDto(unread, incoming, archived).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error summarising introductions for {MemberId}", me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntroductionSummaryDto>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<IntroductionDetailDto>> GetAsync(string id, DateTime? after, AuthData me, DateTime? before = null, int? limit = null)
    {
        try
        {
            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || !IsParty(intro, me.Id) || intro.Status is IntroductionStatuses.Withdrawn)
                return ApiResponseExtensions.ToNotFoundApiResponse<IntroductionDetailDto>("This conversation isn't available.");
            var otherId = OtherId(intro, me.Id);
            var people = await PeopleAsync([otherId]);
            if (!people.TryGetValue(otherId, out var other) || other.Status != "Active")
                return ApiResponseExtensions.ToNotFoundApiResponse<IntroductionDetailDto>("This conversation isn't available.");
            var (iBlocked, blockedMe) = await BlockStateAsync(me.Id, otherId);
            // A request between two people who have blocked each other is never shown; a conversation that already exists stays readable.
            if ((iBlocked || blockedMe) && intro.Status is not (IntroductionStatuses.Accepted or IntroductionStatuses.Ended))
                return ApiResponseExtensions.ToNotFoundApiResponse<IntroductionDetailDto>("This conversation isn't available.");
            var condition = ConditionOf(intro, iBlocked, blockedMe);
            // Someone who declined, or whose request was declined, only ever sees the request while it is open to them.
            if (intro.Status == IntroductionStatuses.Declined && intro.RecipientId == me.Id)
                return ApiResponseExtensions.ToNotFoundApiResponse<IntroductionDetailDto>("This conversation isn't available.");

            var messages = new List<IntroductionMessageDto>();
            bool hasMoreBefore = false, hasMoreAfter = false;
            if (intro.Status is IntroductionStatuses.Accepted or IntroductionStatuses.Ended)
            {
                // Only the page asked for is read and decrypted: the newest, the one before a given message, or what is newer than one.
                var size = Math.Clamp(limit ?? DefaultThreadPage, 1, MaxThreadPage);
                var thread = messageRepo.GetQueryable(m => m.IntroductionId == id);
                List<IntroductionMessage> rows;
                if (after is not null)
                {
                    rows = await thread.Where(m => m.CreatedAt > after).OrderBy(m => m.CreatedAt).Take(MaxThreadPage + 1).ToListAsync();
                    hasMoreAfter = rows.Count > MaxThreadPage;
                    if (hasMoreAfter) rows.RemoveAt(MaxThreadPage);
                }
                else
                {
                    var older = before is null ? thread : thread.Where(m => m.CreatedAt < before);
                    rows = await older.OrderByDescending(m => m.CreatedAt).Take(size + 1).ToListAsync();
                    hasMoreBefore = rows.Count > size;
                    if (hasMoreBefore) rows.RemoveAt(size);
                    rows.Reverse();
                }
                messages = rows.Select(m => new IntroductionMessageDto(m.Id, m.SenderId == me.Id, Read(m.Body, MessageContext(id, m.Id)), m.CreatedAt)).ToList();

                // Opening the conversation marks it read, but only writes when there is something new to mark, and loading older pages is not "opening".
                if (before is null && IsUnread(intro, me.Id))
                {
                    if (intro.RequesterId == me.Id) intro.RequesterReadAt = DateTime.UtcNow; else intro.RecipientReadAt = DateTime.UtcNow;
                    await introRepo.UpdateAsync(intro);
                    if (!iBlocked && !blockedMe) Tell(otherId, "read", id);   // so their "Seen" appears; never to or from someone blocked
                }
                if (before is null && (after is null || messages.Count > 0)) await ClearNoticesAsync(id, me.Id);
            }

            if (intro.Status == IntroductionStatuses.Pending && intro.RecipientId == me.Id) await ClearNoticesAsync(id, me.Id);

            var wasUnread = false; // already cleared above; the list shows the unread dot, the conversation itself does not
            var reopens = intro.Status == IntroductionStatuses.Pending && intro.RecipientId == me.Id ? (await EndedBetweenAsync(me.Id, otherId))?.Id : null;
            var item = Item(intro, me.Id, other, condition) with { Unread = wasUnread, ReopensIntroductionId = reopens };
            var theirRead = (iBlocked || blockedMe) ? null : intro.RequesterId == me.Id ? intro.RecipientReadAt : intro.RequesterReadAt;
            return new IntroductionDetailDto(item, intro.Status == IntroductionStatuses.Accepted && condition == IntroductionConditions.Open, messages, theirRead, hasMoreBefore, hasMoreAfter).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error opening introduction {IntroductionId} for {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntroductionDetailDto>("Something went wrong. Please try again.");
        }
    }

    /// <summary>Reading a conversation also clears its notices in the bell, so the badge never says "new" about something already read.</summary>
    private async Task ClearNoticesAsync(string introductionId, string memberId)
    {
        var unread = (await notificationRepo.GetAllAsync(n => n.RecipientId == memberId && !n.IsRead && n.RelatedEntityType == "Introduction" && (n.RelatedEntityId == introductionId || (n.ActionUrl != null && n.ActionUrl.EndsWith("/connections/" + introductionId))))).ToList();
        if (unread.Count == 0) return;
        foreach (var n in unread) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        await notificationRepo.UpdateRangeAsync(unread);
    }

    public async Task<IApiResponse<object>> RespondAsync(string id, bool accept, AuthData me)
    {
        try
        {
            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || intro.RecipientId != me.Id) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Request not found.");
            if (intro.Status != IntroductionStatuses.Pending) return ApiResponseExtensions.ToConflictApiResponse<object>("This request has already been answered or withdrawn.");
            if (await IsBlockedAsync(me.Id, intro.RequesterId)) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Request not found.");

            var openId = intro.Id;
            if (accept && await EndedBetweenAsync(me.Id, intro.RequesterId, intro.Id) is { } earlier)
            {
                // They had talked before and it was ended: the same conversation comes back, with every message in it. This request has done its job,
                // so it is retired rather than becoming a second conversation with the same person.
                earlier.Status = IntroductionStatuses.Accepted;
                earlier.EndedById = null;
                earlier.LastActivityAt = DateTime.UtcNow;
                earlier.UpdatedBy = me.Id;
                await introRepo.UpdateAsync(earlier);
                intro.Status = IntroductionStatuses.Withdrawn;
                openId = earlier.Id;
            }
            else
            {
                intro.Status = accept ? IntroductionStatuses.Accepted : IntroductionStatuses.Declined;
            }
            intro.RespondedAt = DateTime.UtcNow;
            intro.LastActivityAt = intro.RespondedAt.Value;
            intro.UpdatedBy = me.Id;
            await introRepo.UpdateAsync(intro);

            // A decline is never announced; the person who asked simply gets no reply.
            Tell(me.Id, "changed", openId);   // my own pages refresh either way; a decline is never announced to the other side
            if (accept) Tell(intro.RequesterId, "accepted", openId);
            if (accept)
                await Notify(intro.RequesterId, "Your introduction was accepted", $"{me.Name} accepted. You can now chat privately.", openId);
            return ((object)new IntroductionTargetDto(openId)).ToOkApiResponse(accept ? "Accepted. You can now chat." : "Declined.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error answering introduction {IntroductionId} by {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<object>> WithdrawAsync(string id, AuthData me)
    {
        try
        {
            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || intro.RequesterId != me.Id) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Request not found.");
            if (intro.Status is not (IntroductionStatuses.Pending or IntroductionStatuses.Declined)) return ApiResponseExtensions.ToConflictApiResponse<object>("This request can't be withdrawn.");
            intro.Status = IntroductionStatuses.Withdrawn;
            intro.UpdatedBy = me.Id;
            await introRepo.UpdateAsync(intro);
            Tell(intro.RecipientId, "closed", id);
            Tell(me.Id, "changed", id);
            return new object().ToOkApiResponse("Request withdrawn.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error withdrawing introduction {IntroductionId} by {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<object>> EndAsync(string id, AuthData me)
    {
        try
        {
            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || !IsParty(intro, me.Id)) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Conversation not found.");
            if (intro.Status != IntroductionStatuses.Accepted) return ApiResponseExtensions.ToConflictApiResponse<object>("This conversation has already ended.");
            intro.Status = IntroductionStatuses.Ended;
            intro.EndedById = me.Id;
            intro.LastActivityAt = DateTime.UtcNow;
            intro.UpdatedBy = me.Id;
            await introRepo.UpdateAsync(intro);
            Tell(OtherId(intro, me.Id), "changed", id);
            Tell(me.Id, "changed", id);
            return new object().ToOkApiResponse("Conversation ended.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error ending introduction {IntroductionId} by {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    /// <summary>Reopens a conversation. Only the member who ended it can, and not while either has blocked the other.</summary>
    public async Task<IApiResponse<object>> ResumeAsync(string id, AuthData me)
    {
        try
        {
            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || !IsParty(intro, me.Id)) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Conversation not found.");
            if (intro.Status != IntroductionStatuses.Ended) return ApiResponseExtensions.ToConflictApiResponse<object>("This conversation isn't ended.");
            if (intro.EndedById != me.Id) return ApiResponseExtensions.ToConflictApiResponse<object>("Only the person who ended a conversation can resume it. You can ask to be introduced again.");
            var otherId = OtherId(intro, me.Id);
            var people = await PeopleAsync([otherId]);
            var (iBlocked, blockedMe) = await BlockStateAsync(me.Id, otherId);
            if (!people.TryGetValue(otherId, out var other) || other.Status != "Active" || blockedMe) return ApiResponseExtensions.ToConflictApiResponse<object>(CannotSendNow);
            if (iBlocked) return ApiResponseExtensions.ToConflictApiResponse<object>("Unblock them first to resume this conversation.");
            // If they have since been introduced again, that conversation is the one in use; two open ones with the same person would only split the history.
            if (await introRepo.GetOneAsync(i => i.Id != id && i.Status == IntroductionStatuses.Accepted
                    && ((i.RequesterId == me.Id && i.RecipientId == otherId) || (i.RequesterId == otherId && i.RecipientId == me.Id))) is not null)
                return ApiResponseExtensions.ToConflictApiResponse<object>("You already have an open conversation with them.");

            intro.Status = IntroductionStatuses.Accepted;
            intro.EndedById = null;
            intro.LastActivityAt = DateTime.UtcNow;
            intro.UpdatedBy = me.Id;
            await introRepo.UpdateAsync(intro);
            Tell(otherId, "changed", id);
            Tell(me.Id, "changed", id);
            return new object().ToOkApiResponse("Conversation resumed.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resuming introduction {IntroductionId} by {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<IntroductionMessageDto>> SendMessageAsync(string id, string body, AuthData me)
    {
        try
        {
            var text = (body ?? string.Empty).Trim();
            if (text.Length == 0) return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionMessageDto>("Write a message first.");
            if (text.Length > MaxMessage) return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionMessageDto>($"Please keep a message to {MaxMessage} characters.");

            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || !IsParty(intro, me.Id)) return ApiResponseExtensions.ToNotFoundApiResponse<IntroductionMessageDto>("This conversation isn't available.");
            if (intro.Status == IntroductionStatuses.Pending) return ApiResponseExtensions.ToConflictApiResponse<IntroductionMessageDto>("You can chat once they accept your request.");
            if (intro.Status != IntroductionStatuses.Accepted) return ApiResponseExtensions.ToConflictApiResponse<IntroductionMessageDto>("This conversation has ended.");
            var otherId = OtherId(intro, me.Id);
            var people = await PeopleAsync([otherId]);
            if (!people.TryGetValue(otherId, out var other) || other.Status != "Active" || await IsBlockedAsync(me.Id, otherId))
                return ApiResponseExtensions.ToConflictApiResponse<IntroductionMessageDto>(CannotSendNow);   // the same words whoever blocked, and whatever the reason

            var minuteAgo = DateTime.UtcNow.AddMinutes(-1);
            if (await messageRepo.CountAsync(m => m.SenderId == me.Id && m.CreatedAt > minuteAgo) >= MessagesPerMinute)
                return ApiResponseExtensions.ToBadRequestApiResponse<IntroductionMessageDto>("You're sending messages very quickly. Wait a moment and try again.");

            // Tell the other person only when this is the first message they haven't seen, so a busy chat is one notice, not fifty.
            var theirRead = intro.RequesterId == me.Id ? intro.RecipientReadAt : intro.RequesterReadAt;
            var firstUnread = intro.LastMessageAt is null || intro.LastMessageSenderId != me.Id || (theirRead is not null && theirRead >= intro.LastMessageAt);

            var message = new IntroductionMessage { IntroductionId = id, SenderId = me.Id, CreatedBy = me.Id };
            message.Body = protector.Protect(text, MessageContext(id, message.Id));
            await messageRepo.AddAsync(message);

            intro.LastMessageAt = message.CreatedAt;
            intro.LastActivityAt = message.CreatedAt;
            intro.LastMessageSenderId = me.Id;
            intro.LastMessagePreview = protector.Protect(text.Length > 80 ? text[..80].TrimEnd() + "…" : text, FieldContext(id, PreviewField));
            if (intro.RequesterId == me.Id) intro.RequesterReadAt = message.CreatedAt; else intro.RecipientReadAt = message.CreatedAt;
            await introRepo.UpdateAsync(intro);
            Tell(otherId, "message", id);
            Tell(me.Id, "message", id);   // my other tabs and devices

            if (firstUnread) await Notify(otherId, "New message", $"{me.Name} sent you a message.", id, message.Id);
            return new IntroductionMessageDto(message.Id, true, text, message.CreatedAt).ToCreatedApiResponse("Sent");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending a message in introduction {IntroductionId} by {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<IntroductionMessageDto>("Your message didn't send. Please try again.");
        }
    }

    public async Task<IApiResponse<object>> ReportAsync(string id, ReportIntroductionRequest request, AuthData me)
    {
        try
        {
            if (!ContentReportReasons.All.Contains(request.Reason)) return ApiResponseExtensions.ToBadRequestApiResponse<object>("Please choose a reason.");
            var intro = await introRepo.GetByIdAsync(id);
            if (intro is null || !IsParty(intro, me.Id)) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Conversation not found.");

            var already = await reportRepo.GetOneAsync(r => r.ReporterMemberId == me.Id && r.EntityType == ContentReportTypes.Introduction && r.EntityId == id && r.Status == ContentReportStatuses.Open);
            if (already is null)
            {
                var people = await PeopleAsync([intro.RequesterId, intro.RecipientId]);
                string Name(string pid) => people.TryGetValue(pid, out var p) ? NameOf(p) : "A former member";
                var recent = (await messageRepo.GetQueryable(m => m.IntroductionId == id).OrderByDescending(m => m.CreatedAt).Take(ReportedMessages).ToListAsync()).OrderBy(m => m.CreatedAt);

                // The reporter chooses to send this. An admin never reads a conversation otherwise.
                var lines = new List<string> { string.IsNullOrWhiteSpace(request.Details) ? "" : request.Details.Trim(), "", $"Why they asked: {Read(intro.Reason, FieldContext(intro.Id, ReasonField))}", "", "Recent messages, sent by the reporter with this report:" };
                lines.AddRange(recent.Select(m => $"[{m.CreatedAt:yyyy-MM-dd HH:mm} UTC] {Name(m.SenderId)}: {Read(m.Body, MessageContext(id, m.Id))}"));
                if (!recent.Any()) lines.Add("(no messages)");

                await reportRepo.AddAsync(new ContentReport
                {
                    ReporterMemberId = me.Id,
                    ReporterName = me.Name,
                    EntityType = ContentReportTypes.Introduction,
                    EntityId = id,
                    EntityTitle = $"Conversation between {Name(intro.RequesterId)} and {Name(intro.RecipientId)}",
                    Reason = request.Reason,
                    Details = string.Join("\n", lines).Trim(),
                    CreatedBy = me.Id,
                });
            }
            return new object().ToOkApiResponse("Thank you. Your report has been sent to the administrators.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reporting introduction {IntroductionId} by {MemberId}", id, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<object>> BlockAsync(string memberId, AuthData me)
    {
        try
        {
            if (memberId == me.Id) return ApiResponseExtensions.ToBadRequestApiResponse<object>("You can't block yourself.");
            if (!(await PeopleAsync([memberId])).ContainsKey(memberId)) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found.");
            if (await blockRepo.GetOneAsync(b => b.BlockerId == me.Id && b.BlockedId == memberId) is null)
            {
                await blockRepo.AddAsync(new MemberBlock { BlockerId = me.Id, BlockedId = memberId, CreatedBy = me.Id });
                // Open pages on both sides refresh: the one who blocked keeps the conversation (with an Unblock button), the other sees it as
                // unavailable. Nobody is told why.
                foreach (var i in await introRepo.GetAllAsync(x => (x.RequesterId == me.Id && x.RecipientId == memberId) || (x.RequesterId == memberId && x.RecipientId == me.Id)))
                { Tell(memberId, "changed", i.Id); Tell(me.Id, "changed", i.Id); }
            }
            return new object().ToOkApiResponse("Blocked. You won't send or receive messages from each other.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error blocking {MemberId} by {Me}", memberId, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<object>> UnblockAsync(string memberId, AuthData me)
    {
        try
        {
            var block = await blockRepo.GetOneAsync(b => b.BlockerId == me.Id && b.BlockedId == memberId);
            if (block is not null)
            {
                await blockRepo.RemoveAsync(block);
                // Their open pages refresh too, so a conversation that was unavailable to them simply works again.
                foreach (var i in await introRepo.GetAllAsync(x => (x.RequesterId == me.Id && x.RecipientId == memberId) || (x.RequesterId == memberId && x.RecipientId == me.Id)))
                { Tell(memberId, "changed", i.Id); Tell(me.Id, "changed", i.Id); }
            }
            return new object().ToOkApiResponse("Unblocked.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unblocking {MemberId} by {Me}", memberId, me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Something went wrong. Please try again.");
        }
    }

    public async Task<IApiResponse<List<IntroductionPersonDto>>> ListBlockedAsync(AuthData me)
    {
        try
        {
            var ids = (await blockRepo.GetAllAsync(b => b.BlockerId == me.Id)).Select(b => b.BlockedId).ToList();
            var people = await PeopleAsync(ids);
            return ids.Where(people.ContainsKey).Select(i => Dto(people[i])).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing blocked members for {Me}", me.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<IntroductionPersonDto>>("Something went wrong. Please try again.");
        }
    }
}

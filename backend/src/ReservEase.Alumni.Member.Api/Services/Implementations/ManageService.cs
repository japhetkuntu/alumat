using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Group = ReservEase.Alumni.Member.Api.Services.Implementations.GroupRef;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class ManageService(
    LeadershipAccess access,
    GroupGuard guard,
    GroupMedia media,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<InstitutionAuditLogEntry> auditRepo,
    ICurrentTenantService currentTenant,
    ITemporalClientProvider temporalProvider,
    IConfiguration config,
    ILogger<ManageService> logger) : IManageService
{
    private const int ActiveWindowDays = 30;
    private const int MaxListed = 200;
    private const int MaxEventsListed = 100;
    private const int InsertChunk = 500;
    private const int MaxHistory = 30;
    public const string MessageType = "GroupMessage";
    public const string MessageSentType = "GroupMessageSent";

    private Task<(Group? Group, IApiResponse<T>? Refusal)> GuardAsync<T>(string memberId, string privilege, string scopeType, string scopeId)
        => guard.GuardAsync<T>(memberId, privilege, scopeType, scopeId);

    // ── What I can manage ────────────────────────────────────────────────────

    public async Task<IApiResponse<List<ManageGroupDto>>> GetGroupsAsync(string memberId)
    {
        try
        {
            var held = (await access.GetHeldAsync(memberId)).Where(h => h.Privileges.Count > 0).ToList();
            if (held.Count == 0) return new List<ManageGroupDto>().ToOkApiResponse();

            var batches = (await batchRepo.GetAllAsync(_ => true)).ToDictionary(b => b.Id, b => b);
            var communities = (await communityRepo.GetAllAsync(c => c.IsActive)).ToDictionary(c => c.Id, c => c);
            var found = new Dictionary<(string, string), (string Name, HashSet<string> Roles, HashSet<string> Privileges)>();

            void Add((string Type, string Id) key, string name, HeldRole role)
            {
                if (!found.TryGetValue(key, out var entry)) found[key] = entry = (name, [], []);
                entry.Roles.Add(role.RoleName);
                foreach (var p in role.Privileges) entry.Privileges.Add(p);
            }

            foreach (var h in held)
            {
                // A role for the whole institution applies to every group; any other applies to the one it was given for.
                if (h.ScopeType == LeadershipPost.ScopeInstitution)
                {
                    foreach (var b in batches.Values) Add((LeadershipPost.ScopeBatch, b.Id), b.Name, h);
                    foreach (var c in communities.Values) Add((LeadershipPost.ScopeCommunity, c.Id), c.Name, h);
                }
                else if (h.ScopeType == LeadershipPost.ScopeBatch && batches.TryGetValue(h.ScopeId, out var batch)) Add((h.ScopeType, h.ScopeId), batch.Name, h);
                else if (h.ScopeType == LeadershipPost.ScopeCommunity && communities.TryGetValue(h.ScopeId, out var community)) Add((h.ScopeType, h.ScopeId), community.Name, h);
            }

            // Who is waiting, for the groups where this leader can approve: two small queries however many groups there are.
            var approves = found.Where(kv => kv.Value.Privileges.Contains(LeadershipPrivileges.ManageMembers)).Select(kv => kv.Key).ToList();
            var pendingByYear = new Dictionary<int, int>();
            var pendingByCommunity = new Dictionary<string, int>();
            if (approves.Any(k => k.Item1 == LeadershipPost.ScopeBatch))
                pendingByYear = (await memberRepo.GetAllAsync(m => m.Status == "Pending")).GroupBy(m => m.GraduationYear).ToDictionary(g => g.Key, g => g.Count());
            if (approves.Any(k => k.Item1 == LeadershipPost.ScopeCommunity))
                pendingByCommunity = (await membershipRepo.GetAllAsync(m => m.Status == "Pending")).GroupBy(m => m.CommunityId).ToDictionary(g => g.Key, g => g.Count());
            int Waiting((string, string) key) => !approves.Contains(key) ? 0
                : key.Item1 == LeadershipPost.ScopeBatch ? (batches.TryGetValue(key.Item2, out var b) ? pendingByYear.GetValueOrDefault(b.Year) : 0)
                : pendingByCommunity.GetValueOrDefault(key.Item2);

            return found.OrderBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new ManageGroupDto(kv.Key.Item1, kv.Key.Item2, kv.Value.Name, kv.Value.Roles.Order().ToList(),
                    LeadershipPrivileges.All.Select(p => p.Key).Where(kv.Value.Privileges.Contains).ToList(), Waiting(kv.Key)))
                .ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving groups member {MemberId} can manage", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<ManageGroupDto>>("Failed to load your groups");
        }
    }

    // ── Report ───────────────────────────────────────────────────────────────

    public async Task<IApiResponse<GroupSummaryDto>> GetSummaryAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<GroupSummaryDto>(memberId, LeadershipPrivileges.ViewReport, scopeType, scopeId);
            if (refusal is not null) return refusal;

            var (people, pending) = await PeopleAsync(group!);
            var active = people.Where(p => p.Status == "Active").ToList();
            var since = DateTime.UtcNow.AddDays(-ActiveWindowDays);
            var completion = active.Count == 0 ? 0 : (int)Math.Round(active.Average(Completeness) * 100);
            return new GroupSummaryDto(group!.Name, active.Count, active.Count(p => p.CreatedAt >= since),
                active.Count(p => p.LastLoginAt != null && p.LastLoginAt >= since), completion, pending).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building group summary {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<GroupSummaryDto>("Failed to load the report");
        }
    }

    // ── Members ──────────────────────────────────────────────────────────────

    /// <summary>How many members a group has and how many are waiting, counted over everyone rather than the newest 200 the list shows.</summary>
    public async Task<IApiResponse<GroupPeopleCountsDto>> GetPeopleCountsAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<GroupPeopleCountsDto>(memberId, LeadershipPrivileges.ManageMembers, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var (people, pending) = await PeopleAsync(group!);
            return new GroupPeopleCountsDto(people.Count(p => p.Status == "Active"), pending).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error counting group members {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<GroupPeopleCountsDto>("Failed to count the members");
        }
    }

    public async Task<IApiResponse<List<GroupMemberDto>>> GetMembersAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<List<GroupMemberDto>>(memberId, LeadershipPrivileges.ManageMembers, scopeType, scopeId);
            if (refusal is not null) return refusal;

            var (people, _) = await PeopleAsync(group!);
            return people
                .OrderByDescending(p => p.Status == "Pending").ThenByDescending(p => p.JoinedAt)
                .Take(MaxListed)
                .Select(p => new GroupMemberDto(p.Id, p.Name, p.Photo, p.JobTitle, p.Status, p.JoinedAt, p.Status == "Pending", p.Status == "Pending" && group!.Community is not null))
                .ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing group members {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<GroupMemberDto>>("Failed to load the members");
        }
    }

    public async Task<IApiResponse<object>> DecideAsync(string memberId, string scopeType, string scopeId, string targetMemberId, bool approve)
    {
        try
        {
            var (group, refusal) = await GuardAsync<object>(memberId, LeadershipPrivileges.ManageMembers, scopeType, scopeId);
            if (refusal is not null) return refusal;
            return group!.Community is not null
                ? await DecideCommunityRequestAsync(memberId, group, targetMemberId, approve)
                : await ApproveYearGroupMemberAsync(memberId, group, targetMemberId, approve);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deciding request {Target} in {ScopeType}/{ScopeId}", targetMemberId, scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to save your decision");
        }
    }

    private async Task<IApiResponse<object>> DecideCommunityRequestAsync(string memberId, Group group, string targetMemberId, bool approve)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == group.Id && m.MemberId == targetMemberId);
        if (membership is null || membership.Status != "Pending")
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("There is no waiting request from that member");

        membership.Status = approve ? "Approved" : "Rejected";
        membership.DecidedAt = DateTime.UtcNow;
        membership.DecidedBy = memberId;
        membership.UpdatedAt = DateTime.UtcNow;
        membership.UpdatedBy = memberId;
        await membershipRepo.UpdateAsync(membership);

        await TellAsync(targetMemberId, approve ? "You're in" : "Your request was declined",
            approve ? $"Your request to join \"{group.Name}\" was approved." : $"Your request to join \"{group.Name}\" was declined.",
            MessageType, group.Id, $"/communities/{group.Id}", memberId);
        await AuditAsync(memberId, approve ? "Group Request Approved" : "Group Request Declined", $"{group.Name}: {await NameOfAsync(targetMemberId)}");
        return new object().ToOkApiResponse(approve ? "Approved" : "Declined");
    }

    /// <summary>
    /// Approving a newly registered member of a year group: the same steps an administrator's approval takes (member number, active status,
    /// the welcome notification and email). Declining stays with the administrators, because a decline counts towards blocking an email address.
    /// </summary>
    private async Task<IApiResponse<object>> ApproveYearGroupMemberAsync(string memberId, Group group, string targetMemberId, bool approve)
    {
        if (!approve)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Registrations to a year group can only be declined by the administrators");

        var year = group.Batch!.Year;
        var target = await memberRepo.GetByIdAsync(targetMemberId);
        if (target is null || target.Status != "Pending" || target.GraduationYear != year)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("There is no waiting registration from that member");

        var institution = string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        if (string.IsNullOrEmpty(target.MemberNumber))
        {
            var prefix = MemberNumbers.Prefix(institution?.Slug, institution?.OrganizationType == OrganizationTypes.Community, target.GraduationYear);
            target.MemberNumber = await MemberNumbers.NextAsync(memberRepo.GetQueryable(), prefix);
        }
        target.Status = "Active";
        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedBy = memberId;
        await memberRepo.UpdateAsync(target);

        await EnqueueAsync(NotificationRequest.MemberStatusChanged(currentTenant.InstitutionId ?? "", target.Id, target.FirstName, "Active", null));
        if (institution is not null) await EmailApprovedAsync(institution, target);
        await AuditAsync(memberId, "Member Approved", $"{target.FirstName} {target.LastName} ({target.Email}), {group.Name}");
        return new object().ToOkApiResponse("Approved");
    }

    private async Task EmailApprovedAsync(Institution institution, MemberEntity member)
    {
        var portalUrl = MemberPortalLinks.UrlOrEmpty(institution.Slug, institution.CustomDomain, config["MemberPortalBaseDomain"]);
        var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
        await EnqueueAsync(NotificationRequest.Email(new SendEmailRequest
        {
            To = [new EmailContact { Email = member.Email, Name = member.FirstName }],
            TemplateId = "notification",
            TemplateVariables = new
            {
                first_name = member.FirstName,
                title = "Welcome — You're Approved!",
                body = $"Welcome, {member.FirstName} — your membership has been approved. You now have full access to the portal.",
                badge_label = "Account Update",
                action_url = portalUrl,
                action_label = "Go to your portal",
                pref_label = "your membership status changed",
                brand_name = brandName,
                brand_color = institution.PrimaryColorHex,
                brand_secondary_color = institution.SecondaryColorHex,
                brand_logo = institution.LogoUrl,
            },
        }, $"member approved email to {member.Email}"));
    }

    // ── Message ──────────────────────────────────────────────────────────────

    public async Task<IApiResponse<ManageChannelsDto>> GetChannelsAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<ManageChannelsDto>(memberId, LeadershipPrivileges.MessageGroup, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var institution = await LoadInstitutionAsync();
            var (people, _) = await PeopleAsync(group!);
            var recipients = people.Where(p => p.Status == "Active" && p.Id != memberId).Select(p => p.Id).Distinct().Count();
            var perDay = GroupMessaging.Limit(institution?.GroupMessagesPerDay);
            var left = Math.Max(0, perDay - await SentTodayAsync(memberId, group!));
            return new ManageChannelsDto(institution?.BroadcastEmailAllowed == true, institution?.BroadcastSmsAllowed == true, recipients, left, perDay).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading message channels {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ManageChannelsDto>("Failed to load the options");
        }
    }

    public async Task<IApiResponse<GroupMessageResult>> MessageAsync(string memberId, string scopeType, string scopeId, string text, IReadOnlyCollection<string>? channels = null)
    {
        try
        {
            var (group, refusal) = await GuardAsync<GroupMessageResult>(memberId, LeadershipPrivileges.MessageGroup, scopeType, scopeId);
            if (refusal is not null) return refusal;

            var clean = string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (clean.Length == 0) return ApiResponseExtensions.ToBadRequestApiResponse<GroupMessageResult>("Write a message first");
            if (clean.Length > 500) return ApiResponseExtensions.ToBadRequestApiResponse<GroupMessageResult>("Keep it to 500 characters");

            // Email and text cost the platform money, so each is off until platform staff switch it on for the community. Checked here, on the server.
            var institution = await LoadInstitutionAsync();
            var extras = (channels ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var unknown = extras.Where(c => !c.Equals("Email", StringComparison.OrdinalIgnoreCase) && !c.Equals("Sms", StringComparison.OrdinalIgnoreCase)).ToList();
            if (unknown.Count > 0) return ApiResponseExtensions.ToBadRequestApiResponse<GroupMessageResult>("Choose email or text, or leave both off");
            var blocked = new List<string>();
            if (extras.Any(c => c.Equals("Email", StringComparison.OrdinalIgnoreCase)) && institution?.BroadcastEmailAllowed != true) blocked.Add("email");
            if (extras.Any(c => c.Equals("Sms", StringComparison.OrdinalIgnoreCase)) && institution?.BroadcastSmsAllowed != true) blocked.Add("text");
            if (blocked.Count > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<GroupMessageResult>(
                    $"Messages by {string.Join(" and ", blocked)} are not switched on for your community. Notifications are always available.");

            var sentToday = await SentTodayAsync(memberId, group!);
            var perDay = GroupMessaging.Limit(institution?.GroupMessagesPerDay);
            if (sentToday >= perDay)
                return ApiResponseExtensions.ToBadRequestApiResponse<GroupMessageResult>($"You can send {perDay} {(perDay == 1 ? "message" : "messages")} a day to the same group. Try again tomorrow.");

            var who = await NameOfAsync(memberId, "A leader");
            var (people, _) = await PeopleAsync(group!);
            var recipients = people.Where(p => p.Status == "Active" && p.Id != memberId).GroupBy(p => p.Id).Select(g => g.First()).ToList();

            var rows = recipients.Select(p => new Notification
            {
                RecipientId = p.Id, RecipientType = "Member", Title = group!.Name, Body = $"{who}: {clean}", Type = MessageType,
                RelatedEntityId = group.Id, RelatedEntityType = group.Type, ActionUrl = "/notifications", CreatedBy = memberId,
            }).ToList();
            for (var i = 0; i < rows.Count; i += InsertChunk) await notificationRepo.AddRangeAsync(rows.Skip(i).Take(InsertChunk).ToList());

            // A receipt for the sender: it confirms the message went, and it is what the daily limit counts.
            await notificationRepo.AddAsync(new Notification
            {
                RecipientId = memberId, RecipientType = "Member", Title = $"You messaged {group!.Name}",
                Body = $"{recipients.Count} {(recipients.Count == 1 ? "person" : "people")} will see: {clean}", Type = MessageSentType,
                RelatedEntityId = group.Id, RelatedEntityType = group.Type, ActionUrl = "/notifications", CreatedBy = memberId,
            });

            // The in-app notification is already saved. A phone alert goes to everyone who has the app set up for alerts, and email or text to
            // those the leader chose. The notification itself is not sent again.
            if (recipients.Count > 0 && !string.IsNullOrEmpty(currentTenant.InstitutionId))
                await EnqueueAsync(NotificationRequest.Broadcast(currentTenant.InstitutionId,
                    recipients.Select(p => new BroadcastRecipient(p.Id, p.Email, p.FirstName, p.Phone)).ToList(),
                    group.Name, $"{who}: {clean}", ["Push", .. extras.Select(c => c.Equals("Sms", StringComparison.OrdinalIgnoreCase) ? "Sms" : "Email")]));

            await AuditAsync(memberId, "Group Message Sent", $"{group.Name}: {recipients.Count} {(recipients.Count == 1 ? "person" : "people")}{(extras.Count > 0 ? $" ({string.Join(", ", extras)})" : "")}");
            return new GroupMessageResult(recipients.Count).ToOkApiResponse("Sent");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending group message {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<GroupMessageResult>("Failed to send the message");
        }
    }

    /// <summary>
    /// What has been sent to this group, newest first, by any leader who may message it: so nobody repeats what was just said.
    /// Read from the receipts each send leaves, which hold who sent it, how many people it reached and the words.
    /// </summary>
    public async Task<IApiResponse<List<GroupMessageHistoryDto>>> GetMessagesAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<List<GroupMessageHistoryDto>>(memberId, LeadershipPrivileges.MessageGroup, scopeType, scopeId);
            if (refusal is not null) return refusal;

            var receipts = (await notificationRepo.GetAllAsync(n => n.Type == MessageSentType && n.RelatedEntityId == group!.Id))
                .OrderByDescending(n => n.CreatedAt).Take(MaxHistory).ToList();
            if (receipts.Count == 0) return new List<GroupMessageHistoryDto>().ToOkApiResponse();

            var senderIds = receipts.Select(r => r.RecipientId).Distinct().ToList();
            var names = (await memberRepo.GetAllAsync(m => senderIds.Contains(m.Id))).ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim());
            return receipts.Select(r =>
            {
                // A receipt reads "N people will see: the words". Everything after the first ": " is the message.
                var cut = r.Body.IndexOf(": ", StringComparison.Ordinal);
                var text = cut >= 0 ? r.Body[(cut + 2)..] : r.Body;
                var count = int.TryParse(r.Body.Split(' ', 2)[0], out var n) ? n : 0;
                return new GroupMessageHistoryDto(r.Id, names.GetValueOrDefault(r.RecipientId, "A leader"), text, count, r.CreatedAt, r.RecipientId == memberId);
            }).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading message history {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<GroupMessageHistoryDto>>("Failed to load the messages");
        }
    }

    /// <summary>How many messages this leader has already sent to this group today. The receipts they are left are what is counted.</summary>
    private Task<int> SentTodayAsync(string memberId, Group group)
    {
        var dayStart = DateTime.UtcNow.Date;
        return notificationRepo.CountAsync(n => n.RecipientId == memberId && n.Type == MessageSentType && n.RelatedEntityId == group.Id && n.CreatedAt >= dayStart);
    }

    // ── Events ───────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<GroupEventDto>>> GetEventsAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<List<GroupEventDto>>(memberId, LeadershipPrivileges.ManageEvents, scopeType, scopeId);
            if (refusal is not null) return refusal;

            var events = await EventsOfAsync(group!);
            return events.OrderByDescending(e => e.StartDate).Take(MaxEventsListed).Select(e => ToDto(e, memberId)).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing group events {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<GroupEventDto>>("Failed to load the events");
        }
    }

    public async Task<IApiResponse<GroupEventDto>> CreateEventAsync(string memberId, string scopeType, string scopeId, SaveGroupEventRequest request)
    {
        try
        {
            var (group, refusal) = await GuardAsync<GroupEventDto>(memberId, LeadershipPrivileges.ManageEvents, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var clean = CleanEvent(request, mustBeAhead: true, out var description);
            if (clean.Problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>(clean.Problem);
            if (CleanExtras(request, out var mapLink, out var videos) is { } extra) return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>(extra);
            if (await GroupMedia.CheckAsync(request.BannerImage, request.Images) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>(picture);

            // A group's event is for that group only and is not shown publicly. It is free: taking money goes through the administrators.
            var ev = new AlumniEvent
            {
                CommunityId = group!.Community?.Id,
                YearGroups = group.Batch is { } b ? [b.Year] : null,
                Title = clean.Title, Description = description, Venue = clean.Venue,
                StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
                EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : null,
                Capacity = request.Capacity, Status = "Upcoming", IsTicketed = false, IsPublic = false, CreatedBy = memberId,
                GoogleLocationUrl = mapLink, YoutubeVideoUrls = videos,
                BannerImageUrl = request.BannerImage is null ? null : await media.UploadAsync(request.BannerImage),
            };
            var gallery = await media.UploadAllAsync(request.Images);
            ev.ImageUrls = gallery.Count > 0 ? gallery : null;
            await eventRepo.AddAsync(ev);
            await EnqueueAsync(NotificationRequest.EventReminder(currentTenant.InstitutionId ?? "", ev.Id));
            await AuditAsync(memberId, "Event Created", $"{ev.Title}, {group.Name}");
            return ToDto(ev, memberId).ToCreatedApiResponse("Event created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating group event {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<GroupEventDto>("Failed to create the event");
        }
    }

    public async Task<IApiResponse<GroupEventDto>> UpdateEventAsync(string memberId, string scopeType, string scopeId, string eventId, SaveGroupEventRequest request)
    {
        try
        {
            var (group, refusal) = await GuardAsync<GroupEventDto>(memberId, LeadershipPrivileges.ManageEvents, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var ev = await OwnEventAsync(group!, memberId, eventId);
            if (ev is null) return ApiResponseExtensions.ToNotFoundApiResponse<GroupEventDto>("Event not found");
            if (ev.Status is "Cancelled" or "Completed") return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>("This event is already over");

            var start = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
            var clean = CleanEvent(request, mustBeAhead: start != ev.StartDate, out var description);
            if (clean.Problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>(clean.Problem);
            if (CleanExtras(request, out var mapLink, out var videos) is { } extra) return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>(extra);
            var kept = GroupMedia.Keep(ev.ImageUrls, request.ExistingImageUrls, []);
            if (await GroupMedia.CheckAsync(request.BannerImage, request.Images, kept.Count) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupEventDto>(picture);

            var end = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null;
            var detailsChanged = ev.StartDate != start || ev.EndDate != end || ev.Venue != clean.Venue || ev.Capacity != request.Capacity;
            ev.Title = clean.Title; ev.Description = description; ev.Venue = clean.Venue; ev.StartDate = start; ev.EndDate = end; ev.Capacity = request.Capacity;
            ev.GoogleLocationUrl = mapLink; ev.YoutubeVideoUrls = videos;
            ev.BannerImageUrl = request.BannerImage is not null ? await media.UploadAsync(request.BannerImage) : request.RemoveBanner ? null : ev.BannerImageUrl;
            var all = kept.Concat(await media.UploadAllAsync(request.Images)).ToList();
            ev.ImageUrls = all.Count > 0 ? all : null;
            ev.UpdatedAt = DateTime.UtcNow; ev.UpdatedBy = memberId;
            await eventRepo.UpdateAsync(ev);

            if (detailsChanged)
                foreach (var rsvp in await rsvpRepo.GetAllAsync(r => r.EventId == ev.Id && r.Status == EventRsvpStatuses.Confirmed))
                    await EnqueueAsync(NotificationRequest.EventDetailsChanged(ev.InstitutionId, rsvp.MemberId, ev.Id, ev.Title));
            await AuditAsync(memberId, "Event Updated", $"{ev.Title}, {group!.Name}");
            return ToDto(ev, memberId).ToOkApiResponse("Event updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating group event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<GroupEventDto>("Failed to update the event");
        }
    }

    public async Task<IApiResponse<object>> CancelEventAsync(string memberId, string scopeType, string scopeId, string eventId)
    {
        try
        {
            var (group, refusal) = await GuardAsync<object>(memberId, LeadershipPrivileges.ManageEvents, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var ev = await OwnEventAsync(group!, memberId, eventId);
            if (ev is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            if (ev.Status is "Cancelled" or "Completed") return ApiResponseExtensions.ToBadRequestApiResponse<object>("This event is already over");

            ev.Status = "Cancelled"; ev.UpdatedAt = DateTime.UtcNow; ev.UpdatedBy = memberId;
            await eventRepo.UpdateAsync(ev);
            foreach (var rsvp in await rsvpRepo.GetAllAsync(r => r.EventId == ev.Id && r.Status == EventRsvpStatuses.Confirmed))
                await EnqueueAsync(NotificationRequest.EventCancelled(ev.InstitutionId, rsvp.MemberId, ev.Id, ev.Title));
            await AuditAsync(memberId, "Event Cancelled", $"{ev.Title}, {group!.Name}");
            return new object().ToOkApiResponse("Event cancelled");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error cancelling group event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to cancel the event");
        }
    }

    /// <summary>The group's events: a community's own, or the ones aimed at a year group. Few enough per institution to filter after loading.</summary>
    private async Task<List<AlumniEvent>> EventsOfAsync(Group group)
    {
        if (group.Community is not null) return (await eventRepo.GetAllAsync(e => e.CommunityId == group.Id)).ToList();
        var year = group.Batch!.Year;
        return (await eventRepo.GetAllAsync(e => e.CommunityId == null)).Where(e => e.YearGroups != null && e.YearGroups.Contains(year)).ToList();
    }

    /// <summary>An event the leader made, in this group. Someone else's event, or one in another group, is simply not found.</summary>
    private async Task<AlumniEvent?> OwnEventAsync(Group group, string memberId, string eventId)
        => (await EventsOfAsync(group)).FirstOrDefault(e => e.Id == eventId && e.CreatedBy == memberId && !e.IsTicketed);

    private static GroupEventDto ToDto(AlumniEvent e, string memberId)
        => new(e.Id, e.Title, e.Description, e.Venue, e.StartDate, e.EndDate, e.Capacity, e.RsvpCount, e.Status,
            e.CreatedBy == memberId && !e.IsTicketed && e.Status is not ("Cancelled" or "Completed"),
            e.BannerImageUrl, e.ImageUrls, e.YoutubeVideoUrls, e.GoogleLocationUrl);

    private static string? CleanExtras(SaveGroupEventRequest r, out string? mapLink, out List<string>? videos)
    {
        videos = null;
        return GroupMedia.CleanWebLink(r.GoogleLocationUrl, out mapLink) ?? GroupMedia.CleanVideos(r.YoutubeVideoUrls, out videos);
    }

    private static (string Title, string Venue, string? Problem) CleanEvent(SaveGroupEventRequest r, bool mustBeAhead, out string? description)
    {
        description = null;
        var title = string.Join(" ", (r.Title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var venue = string.Join(" ", (r.Venue ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (title.Length == 0) return (title, venue, "Give the event a title");
        if (title.Length > 150) return (title, venue, "Keep the title to 150 characters");
        if (venue.Length == 0) return (title, venue, "Say where it will be");
        if (venue.Length > 200) return (title, venue, "Keep the venue to 200 characters");
        if (mustBeAhead && r.StartDate.ToUniversalTime() < DateTime.UtcNow) return (title, venue, "Choose a start that is still ahead");
        if (r.EndDate.HasValue && r.EndDate.Value < r.StartDate) return (title, venue, "It can't end before it starts");
        if (r.Capacity is < 1 or > 100_000) return (title, venue, "Capacity must be a whole number of at least 1");
        description = RichText.Sanitize(r.Description);
        if (RichText.IsTooLong(description)) return (title, venue, RichText.TooLongMessage);
        return (title, venue, null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record Person(string Id, string Name, string FirstName, string Email, string? Phone, string? Photo, string? JobTitle, string Status, DateTime JoinedAt, DateTime CreatedAt,
        DateTime? LastLoginAt, string? Location, string? Bio, string? LinkedInUrl, string? Company);

    private static double Completeness(Person p)
    {
        var filled = new[] { p.Photo, p.JobTitle, p.Location, p.Bio, p.LinkedInUrl, p.Company }.Count(v => !string.IsNullOrWhiteSpace(v));
        return filled / 6.0;
    }

    private static Person ToPerson(MemberEntity m, string status, DateTime joinedAt)
        => new(m.Id, $"{m.FirstName} {m.LastName}".Trim(), m.FirstName, m.Email, m.Phone, m.ProfilePictureUrl, m.JobTitle, status, joinedAt, m.CreatedAt,
            m.LastLoginAt, m.Location, m.Bio, m.LinkedInUrl, m.Company);

    /// <summary>Who is in the group and how many are waiting. A year group is the members of that graduating year; a community is the people it has accepted or has waiting.</summary>
    private async Task<(List<Person> People, int Pending)> PeopleAsync(Group group)
    {
        if (group.Batch is { } batch)
        {
            var year = batch.Year;
            var members = (await memberRepo.GetAllAsync(m => m.GraduationYear == year && (m.Status == "Active" || m.Status == "Pending"))).ToList();
            return (members.Select(m => ToPerson(m, m.Status, m.CreatedAt)).ToList(), members.Count(m => m.Status == "Pending"));
        }

        var groupId = group.Id;
        var memberships = (await membershipRepo.GetAllAsync(m => m.CommunityId == groupId && (m.Status == "Approved" || m.Status == "Pending"))).ToList();
        var ids = memberships.Select(m => m.MemberId).Distinct().ToList();
        var byId = (await memberRepo.GetAllAsync(m => ids.Contains(m.Id) && m.Status == "Active")).ToDictionary(m => m.Id);
        var people = memberships.Where(m => byId.ContainsKey(m.MemberId))
            .Select(m => ToPerson(byId[m.MemberId], m.Status == "Approved" ? "Active" : "Pending", m.DecidedAt ?? m.RequestedAt)).ToList();
        return (people, people.Count(p => p.Status == "Pending"));
    }

    private Task<Institution?> LoadInstitutionAsync()
        => string.IsNullOrEmpty(currentTenant.InstitutionId) ? Task.FromResult<Institution?>(null) : institutionRepo.GetByIdAsync(currentTenant.InstitutionId);

    private async Task<string> NameOfAsync(string memberId, string fallback = "A member")
        => await memberRepo.GetByIdAsync(memberId) is { } m ? $"{m.FirstName} {m.LastName}".Trim() : fallback;

    /// <summary>Queues a notification. The change it announces is already saved, so a queue that is down must not undo it.</summary>
    private async Task EnqueueAsync(NotificationRequest request)
    {
        try { await temporalProvider.EnqueueNotificationAsync(request, logger); }
        catch (Exception e) { logger.LogWarning(e, "Could not queue a {Kind} notification", request.Kind); }
    }

    /// <summary>What a leader did, for the institution's audit log, under their own name with "(leader)" so it reads differently from an administrator's action.</summary>
    private async Task AuditAsync(string memberId, string action, string target)
    {
        try
        {
            await auditRepo.AddAsync(new InstitutionAuditLogEntry
            {
                ActorId = memberId, Actor = $"{await NameOfAsync(memberId, "A leader")} (leader)", Action = action, Target = target, CreatedBy = memberId,
            });
        }
        catch (Exception e) { logger.LogWarning(e, "Could not record {Action} in the audit log", action); }
    }

    private async Task TellAsync(string memberId, string title, string body, string type, string groupId, string actionUrl, string by)
    {
        try
        {
            await notificationRepo.AddAsync(new Notification
            {
                RecipientId = memberId, RecipientType = "Member", Title = title, Body = body, Type = type,
                RelatedEntityId = groupId, RelatedEntityType = "Community", ActionUrl = actionUrl, CreatedBy = by,
            });
        }
        catch (Exception e)
        {
            // The decision is already saved; a missed notification must not undo it.
            logger.LogWarning(e, "Could not notify member {MemberId} about their request", memberId);
        }
    }
}

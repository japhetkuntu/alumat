using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Polls;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// Running an election from the admin side: building the ballot, publishing it, and publishing the results. SuperAdmin only (see the
/// controller). Once anyone has voted the ballot and audience are fixed, so nobody can reshape a vote that is under way.
/// </summary>
public class PollService(
    IAlumniPgRepository<Poll> pollRepo,
    IAlumniPgRepository<PollPosition> positionRepo,
    IAlumniPgRepository<PollAspirant> aspirantRepo,
    IAlumniPgRepository<PollParticipation> participationRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    PollEngine engine,
    IInstitutionAuditLogService auditLog,
    ICurrentTenantService currentTenant,
    ILogger<PollService> logger) : IPollService
{
    private const int MaxPositions = 20;
    private const int MaxAspirantsPerPosition = 30;
    private const int NotifyBatch = 500;

    // ── Reading ─────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<PollListItem>>> GetPollsAsync(bool archived)
    {
        try
        {
            var now = DateTime.UtcNow;
            var polls = (await pollRepo.GetAllAsync(p => archived ? p.ArchivedAt != null : p.ArchivedAt == null))
                .OrderByDescending(p => p.ClosesAt).ToList();
            if (polls.Count == 0) return new List<PollListItem>().ToOkApiResponse();

            var ids = polls.Select(p => p.Id).ToList();
            var positionCounts = (await positionRepo.GetQueryable(p => ids.Contains(p.PollId)).GroupBy(p => p.PollId).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
            var voted = (await participationRepo.GetQueryable(p => ids.Contains(p.PollId)).GroupBy(p => p.PollId).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
            var names = await GroupNamesAsync(polls);

            // How many members each audience holds is the same for every poll aimed at it, so each distinct audience is counted once.
            var eligibleByAudience = new Dictionary<string, int>();
            async Task<int> EligibleAsync(Poll poll)
            {
                var key = $"{poll.CommunityId}|{string.Join(',', (poll.YearGroups ?? []).OrderBy(y => y))}";
                if (!eligibleByAudience.TryGetValue(key, out var n)) eligibleByAudience[key] = n = await engine.EligibleCountAsync(poll);
                return n;
            }

            var items = new List<PollListItem>();
            foreach (var p in polls)
            {
                var v = voted.GetValueOrDefault(p.Id);
                items.Add(new PollListItem(
                    p.Id, p.Title, p.Status, PollRules.StateOf(p.Status, p.OpensAt, p.ClosesAt, now), p.OpensAt, p.ClosesAt,
                    p.YearGroups, p.CommunityId, p.CommunityId is null ? null : names.GetValueOrDefault(p.CommunityId),
                    p.ResultsMode, p.ResultsPublishedAt, positionCounts.GetValueOrDefault(p.Id), v,
                    v > 0 || p.Status == PollStatuses.Published ? await EligibleAsync(p) : 0,
                    p.ArchivedAt is not null, v == 0));
            }
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing polls");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<PollListItem>>("Failed to load polls");
        }
    }

    public async Task<IApiResponse<PollDetail>> GetPollAsync(string id)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null) return ApiResponseExtensions.ToNotFoundApiResponse<PollDetail>("Poll not found");
            return (await ToDetailAsync(poll)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollDetail>("Failed to load the poll");
        }
    }

    // ── Writing ─────────────────────────────────────────────────────────────

    public async Task<IApiResponse<PollDetail>> CreatePollAsync(SavePollRequest request, AuthData admin)
    {
        try
        {
            var error = await ValidateBasicsAsync(request);
            if (error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>(error);
            var structureError = ValidateStructure(request.Positions, requireComplete: false);
            if (structureError is not null) return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>(structureError);

            var yearGroups = request.YearGroups; var communityId = string.IsNullOrWhiteSpace(request.CommunityId) ? null : request.CommunityId;
            Extensions.ScopeAuthorizationExtensions.NormalizeAudience(ref yearGroups, ref communityId);
            var poll = new Poll
            {
                InstitutionId = currentTenant.InstitutionId ?? string.Empty,
                Title = request.Title.Trim(), Description = Clean(request.Description, 4000),
                OpensAt = request.OpensAt.ToUniversalTime(), ClosesAt = request.ClosesAt.ToUniversalTime(),
                YearGroups = yearGroups is { Count: > 0 } ? yearGroups : null, CommunityId = communityId,
                ResultsMode = request.ResultsMode, Status = PollStatuses.Draft, CreatedBy = admin.Id,
            };
            await pollRepo.AddAsync(poll);
            await WriteStructureAsync(poll, request.Positions ?? []);
            await auditLog.LogAsync(admin, "Poll Created", poll.Title);
            return (await ToDetailAsync(poll)).ToOkApiResponse("Poll saved as a draft");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating poll");
            return ApiResponseExtensions.ToServerErrorApiResponse<PollDetail>("Failed to save the poll");
        }
    }

    public async Task<IApiResponse<PollDetail>> UpdatePollAsync(string id, SavePollRequest request, AuthData admin)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null) return ApiResponseExtensions.ToNotFoundApiResponse<PollDetail>("Poll not found");
            var now = DateTime.UtcNow;
            var state = PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, now);
            if (state == PollStates.Closed)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("This poll has closed, so it can no longer be changed. You can archive it.");

            var error = await ValidateBasicsAsync(request);
            if (error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>(error);

            var voted = await participationRepo.CountAsync(p => p.PollId == id);
            var locked = voted > 0;

            var yearGroups = request.YearGroups; var communityId = string.IsNullOrWhiteSpace(request.CommunityId) ? null : request.CommunityId;
            Extensions.ScopeAuthorizationExtensions.NormalizeAudience(ref yearGroups, ref communityId);
            var sameAudience = communityId == poll.CommunityId
                && (yearGroups ?? []).OrderBy(y => y).SequenceEqual((poll.YearGroups ?? []).OrderBy(y => y));
            if (locked && (request.Positions is not null || !sameAudience))
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("Voting has started, so the positions, aspirants and audience can no longer change. You can still change the closing time and the wording.");

            var opens = request.OpensAt.ToUniversalTime();
            if (state is PollStates.Open && opens != poll.OpensAt)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("This poll is already open, so its opening time can no longer change.");
            if (poll.Status == PollStatuses.Published && request.ClosesAt.ToUniversalTime() <= now)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("Choose a closing time in the future.");
            if (poll.ResultsPublishedAt is not null && request.ResultsMode != poll.ResultsMode)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("The results have been published, so how they are shown can no longer change.");

            if (request.Positions is not null)
            {
                var structureError = ValidateStructure(request.Positions, requireComplete: poll.Status == PollStatuses.Published);
                if (structureError is not null) return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>(structureError);
            }

            var previousYears = poll.YearGroups; var previousCommunity = poll.CommunityId;
            poll.Title = request.Title.Trim();
            poll.Description = Clean(request.Description, 4000);
            poll.OpensAt = opens;
            poll.ClosesAt = request.ClosesAt.ToUniversalTime();
            poll.ResultsMode = request.ResultsMode;
            if (!locked) { poll.YearGroups = yearGroups is { Count: > 0 } ? yearGroups : null; poll.CommunityId = communityId; }
            poll.UpdatedAt = now; poll.UpdatedBy = admin.Id;
            try
            {
                // The poll and its whole ballot change together or not at all.
                await pollRepo.RunInTransactionAsync(async () =>
                {
                    await pollRepo.UpdateAsync(poll);
                    if (request.Positions is not null) await WriteStructureAsync(poll, request.Positions);
                });
            }
            catch (Exception e) when (DbErrors.IsForeignKeyViolation(e) || DbErrors.IsDeadlock(e))
            {
                // A vote landed after the check above. The database refused to remove choices that have votes on them, and rolled everything back.
                return ApiResponseExtensions.ToConflictApiResponse<PollDetail>("Someone voted a moment ago, so the ballot can no longer change. Reload the poll to see it as it is.");
            }

            // The same race for the audience: if a vote slipped in while it was being changed, put it back.
            if (!locked && !sameAudience && await participationRepo.CountAsync(p => p.PollId == id) > 0)
            {
                poll.YearGroups = previousYears; poll.CommunityId = previousCommunity;
                await pollRepo.UpdateAsync(poll);
                return ApiResponseExtensions.ToConflictApiResponse<PollDetail>("Someone voted a moment ago, so the audience can no longer change. Reload the poll to see it as it is.");
            }

            await auditLog.LogAsync(admin, "Poll Updated", poll.Title);
            return (await ToDetailAsync(poll)).ToOkApiResponse("Poll updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollDetail>("Failed to update the poll");
        }
    }

    public async Task<IApiResponse<PollDetail>> PublishAsync(string id, AuthData admin)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null) return ApiResponseExtensions.ToNotFoundApiResponse<PollDetail>("Poll not found");
            if (poll.Status == PollStatuses.Published)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("This poll is already published.");
            var now = DateTime.UtcNow;
            if (poll.ClosesAt <= now)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("The closing time has passed. Choose a time in the future before publishing.");

            var problem = await CheckReadyAsync(poll);
            if (problem is not null) return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>(problem);

            poll.Status = PollStatuses.Published; poll.PublishedAt = now; poll.UpdatedAt = now; poll.UpdatedBy = admin.Id;
            await pollRepo.UpdateAsync(poll);
            await auditLog.LogAsync(admin, "Poll Published", poll.Title);

            var opensLater = poll.OpensAt > now;
            await NotifyEligibleAsync(poll, "PollOpen", "A new vote is open for you",
                opensLater ? $"{poll.Title}: voting opens {poll.OpensAt:MMMM d} and closes {poll.ClosesAt:MMMM d}." : $"{poll.Title}: voting closes {poll.ClosesAt:MMMM d, h:mm tt} UTC. It takes a minute.");
            return (await ToDetailAsync(poll)).ToOkApiResponse("Poll published");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error publishing poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollDetail>("Failed to publish the poll");
        }
    }

    public async Task<IApiResponse<PollDetail>> UnpublishAsync(string id, AuthData admin)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null) return ApiResponseExtensions.ToNotFoundApiResponse<PollDetail>("Poll not found");
            if (poll.Status != PollStatuses.Published)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("This poll is already a draft.");
            if (await participationRepo.CountAsync(p => p.PollId == id) > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("Voting has started, so this poll can no longer go back to a draft.");

            var publishedAt = poll.PublishedAt;
            poll.Status = PollStatuses.Draft; poll.PublishedAt = null; poll.ClosingReminderSentAt = null; poll.UpdatedAt = DateTime.UtcNow; poll.UpdatedBy = admin.Id;
            await pollRepo.UpdateAsync(poll);
            // A vote may have landed between the check above and the save. A draft cannot hold votes, so if one did, undo.
            if (await participationRepo.CountAsync(p => p.PollId == id) > 0)
            {
                poll.Status = PollStatuses.Published; poll.PublishedAt = publishedAt;
                await pollRepo.UpdateAsync(poll);
                return ApiResponseExtensions.ToConflictApiResponse<PollDetail>("Someone voted a moment ago, so this poll can no longer go back to a draft.");
            }
            await auditLog.LogAsync(admin, "Poll Returned To Draft", poll.Title);
            return (await ToDetailAsync(poll)).ToOkApiResponse("Poll is a draft again");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unpublishing poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollDetail>("Failed to update the poll");
        }
    }

    public async Task<IApiResponse<object>> DeleteAsync(string id, AuthData admin)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Poll not found");
            if (await participationRepo.CountAsync(p => p.PollId == id) > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Members have voted in this poll, so it cannot be deleted. You can archive it once it has closed.");

            try
            {
                await pollRepo.RunInTransactionAsync(async () =>
                {
                    await aspirantRepo.ExecuteDeleteAsync(a => a.PollId == id);
                    await positionRepo.ExecuteDeleteAsync(p => p.PollId == id);
                    await pollRepo.ExecuteDeleteAsync(p => p.Id == id);
                });
            }
            catch (Exception e) when (DbErrors.IsForeignKeyViolation(e) || DbErrors.IsDeadlock(e))
            {
                return ApiResponseExtensions.ToConflictApiResponse<object>("Someone voted a moment ago, so this poll can no longer be deleted.");
            }
            await auditLog.LogAsync(admin, "Poll Deleted", poll.Title);
            return new object().ToOkApiResponse("Poll deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete the poll");
        }
    }

    public async Task<IApiResponse<PollDetail>> SetArchivedAsync(string id, bool archived, AuthData admin)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null) return ApiResponseExtensions.ToNotFoundApiResponse<PollDetail>("Poll not found");
            if (archived && PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, DateTime.UtcNow) != PollStates.Closed)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollDetail>("Only a poll that has closed can be archived.");

            poll.ArchivedAt = archived ? DateTime.UtcNow : null; poll.UpdatedAt = DateTime.UtcNow; poll.UpdatedBy = admin.Id;
            await pollRepo.UpdateAsync(poll);
            await auditLog.LogAsync(admin, archived ? "Poll Archived" : "Poll Restored", poll.Title);
            return (await ToDetailAsync(poll)).ToOkApiResponse(archived ? "Poll archived" : "Poll restored");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error archiving poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollDetail>("Failed to update the poll");
        }
    }

    // ── Results ─────────────────────────────────────────────────────────────

    public async Task<IApiResponse<PollResultsDto>> GetResultsAsync(string id)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null || poll.Status != PollStatuses.Published) return ApiResponseExtensions.ToNotFoundApiResponse<PollResultsDto>("Results are not available until the poll is published");
            return (await BuildResultsAsync(poll)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading results for poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollResultsDto>("Failed to load the results");
        }
    }

    public async Task<IApiResponse<PollResultsDto>> PublishResultsAsync(string id, AuthData admin)
    {
        try
        {
            var poll = await pollRepo.GetByIdAsync(id);
            if (poll is null || poll.Status != PollStatuses.Published) return ApiResponseExtensions.ToNotFoundApiResponse<PollResultsDto>("Poll not found");
            if (poll.ResultsMode != PollResultsModes.Manual)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollResultsDto>("This poll shows its results automatically. Only a poll set to publish results by hand needs this.");
            if (PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, DateTime.UtcNow) != PollStates.Closed)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollResultsDto>("Voting has not closed yet. Results can be published once it has.");
            if (poll.ResultsPublishedAt is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<PollResultsDto>("The results are already published.");

            poll.ResultsPublishedAt = DateTime.UtcNow; poll.UpdatedAt = DateTime.UtcNow; poll.UpdatedBy = admin.Id;
            await pollRepo.UpdateAsync(poll);
            await auditLog.LogAsync(admin, "Poll Results Published", poll.Title);
            await NotifyEligibleAsync(poll, "PollResults", "The results are in", $"{poll.Title}: see how the vote went.");
            return (await BuildResultsAsync(poll)).ToOkApiResponse("Results published to members");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error publishing results for poll {PollId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PollResultsDto>("Failed to publish the results");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<PollResultsDto> BuildResultsAsync(Poll poll)
    {
        var now = DateTime.UtcNow;
        var state = PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, now);
        var tally = await engine.TallyAsync(poll);
        // An admin sees the figures once voting has closed, or at any time on a live poll. Never mid-vote on the others.
        var figures = state == PollStates.Closed || (state == PollStates.Open && poll.ResultsMode == PollResultsModes.Live);
        var shown = PollRules.ResultsVisible(poll.Status, poll.ResultsMode, poll.OpensAt, poll.ClosesAt, poll.ResultsPublishedAt, now);
        var positions = figures
            ? tally.Positions.Select(p => new PollPositionResult(p.PositionId, p.Title, p.IsYesNo, p.TotalVotes, p.Abstained,
                p.Options.Select(o => new PollOptionResult(o.AspirantId, o.Name, o.PhotoUrl, o.Votes, o.Percent, o.Leads)).ToList(), p.Yes, p.No, p.Passed, p.Tied)).ToList()
            : [];
        var canPublish = poll.ResultsMode == PollResultsModes.Manual && state == PollStates.Closed && poll.ResultsPublishedAt is null;
        return new PollResultsDto(poll.Id, poll.Title, state, tally.Voted, tally.Eligible, figures, shown, poll.ResultsMode, poll.ResultsPublishedAt, canPublish, positions);
    }

    private async Task<PollDetail> ToDetailAsync(Poll poll)
    {
        var positions = (await positionRepo.GetAllAsync(p => p.PollId == poll.Id)).OrderBy(p => p.SortOrder).ToList();
        var aspirants = (await aspirantRepo.GetAllAsync(a => a.PollId == poll.Id)).OrderBy(a => a.SortOrder).ToList();
        var voted = await participationRepo.CountAsync(p => p.PollId == poll.Id);
        var names = await GroupNamesAsync([poll]);
        var dto = positions.Select(p =>
        {
            var mine = aspirants.Where(a => a.PositionId == p.Id).Select(a => new PollAspirantDto(a.Id, a.Name, a.Details, a.PhotoUrl, a.MemberId)).ToList();
            return new PollPositionDto(p.Id, p.Title, p.Description, PollRules.IsYesNo(mine.Count), mine);
        }).ToList();
        return new PollDetail(
            poll.Id, poll.Title, poll.Description, poll.Status, PollRules.StateOf(poll.Status, poll.OpensAt, poll.ClosesAt, DateTime.UtcNow),
            poll.OpensAt, poll.ClosesAt, poll.YearGroups, poll.CommunityId, poll.CommunityId is null ? null : names.GetValueOrDefault(poll.CommunityId),
            poll.ResultsMode, poll.ResultsPublishedAt, poll.PublishedAt, poll.ArchivedAt is not null, voted, await engine.EligibleCountAsync(poll),
            voted > 0, voted == 0, dto);
    }

    private async Task<Dictionary<string, string>> GroupNamesAsync(IEnumerable<Poll> polls)
    {
        var ids = polls.Where(p => p.CommunityId != null).Select(p => p.CommunityId!).Distinct().ToList();
        return ids.Count == 0 ? [] : (await communityRepo.GetAllAsync(c => ids.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Name);
    }

    private async Task<string?> ValidateBasicsAsync(SavePollRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "Give the poll a title.";
        if (r.Title.Trim().Length > 150) return "The title can be at most 150 characters.";
        if (r.ClosesAt <= r.OpensAt) return "Voting must close after it opens.";
        if (!PollResultsModes.IsValid(r.ResultsMode)) return "Choose when results are shown.";
        if (r.YearGroups is { Count: > 0 } && r.YearGroups.Any(y => y < 1900 || y > DateTime.UtcNow.Year + 10)) return "One of the year groups is not a valid year.";
        if (!string.IsNullOrWhiteSpace(r.CommunityId) && await communityRepo.GetByIdAsync(r.CommunityId) is null) return "That group could not be found.";
        return null;
    }

    private static string? ValidateStructure(List<PollPositionInput>? positions, bool requireComplete)
    {
        if (positions is null) return null;
        if (positions.Count > MaxPositions) return $"A poll can have at most {MaxPositions} positions.";
        if (requireComplete && positions.Count == 0) return "Add at least one position.";
        foreach (var p in positions)
        {
            if (string.IsNullOrWhiteSpace(p.Title)) return "Every position needs a title.";
            if (p.Title.Trim().Length > 120) return "A position title can be at most 120 characters.";
            if (p.Aspirants.Count > MaxAspirantsPerPosition) return $"A position can have at most {MaxAspirantsPerPosition} aspirants.";
            if (requireComplete && p.Aspirants.Count == 0) return $"\"{p.Title.Trim()}\" has no aspirants yet.";
            if (p.Aspirants.Any(a => string.IsNullOrWhiteSpace(a.Name))) return $"Every aspirant for \"{p.Title.Trim()}\" needs a name.";
        }
        return null;
    }

    /// <summary>Whether a saved draft is complete enough to open to members. Returns what is missing, or null.</summary>
    private async Task<string?> CheckReadyAsync(Poll poll)
    {
        var positions = await positionRepo.GetAllAsync(p => p.PollId == poll.Id);
        if (!positions.Any()) return "Add at least one position before publishing.";
        var aspirants = await aspirantRepo.GetAllAsync(a => a.PollId == poll.Id);
        foreach (var p in positions)
            if (!aspirants.Any(a => a.PositionId == p.Id)) return $"\"{p.Title}\" has no aspirants yet.";
        if (await engine.EligibleCountAsync(poll) == 0) return "Nobody is eligible to vote with this audience. Check the year groups or group you chose.";
        return null;
    }

    /// <summary>Replaces the whole ballot. Only called while nobody has voted, so nothing is lost.</summary>
    private async Task WriteStructureAsync(Poll poll, List<PollPositionInput> input)
    {
        // Two statements however big the ballot is, not one round trip per row. The database refuses if any of it already has a vote on it.
        await aspirantRepo.ExecuteDeleteAsync(a => a.PollId == poll.Id);
        await positionRepo.ExecuteDeleteAsync(p => p.PollId == poll.Id);

        var positions = new List<PollPosition>();
        var aspirants = new List<PollAspirant>();
        for (var i = 0; i < input.Count; i++)
        {
            var position = new PollPosition
            {
                InstitutionId = poll.InstitutionId, PollId = poll.Id, SortOrder = i,
                Title = input[i].Title.Trim(), Description = Clean(input[i].Description, 1000), CreatedBy = poll.CreatedBy,
            };
            positions.Add(position);
            for (var j = 0; j < input[i].Aspirants.Count; j++)
            {
                var a = input[i].Aspirants[j];
                aspirants.Add(new PollAspirant
                {
                    InstitutionId = poll.InstitutionId, PollId = poll.Id, PositionId = position.Id, SortOrder = j,
                    Name = a.Name.Trim(), Details = Clean(a.Details, 2000), PhotoUrl = string.IsNullOrWhiteSpace(a.PhotoUrl) ? null : a.PhotoUrl.Trim(),
                    MemberId = string.IsNullOrWhiteSpace(a.MemberId) ? null : a.MemberId.Trim(), CreatedBy = poll.CreatedBy,
                });
            }
        }
        if (positions.Count > 0) await positionRepo.AddRangeAsync(positions);
        if (aspirants.Count > 0) await aspirantRepo.AddRangeAsync(aspirants);
    }

    /// <summary>One in-app notification to each eligible member, in batches so a big institution does not become one huge write.</summary>
    private async Task NotifyEligibleAsync(Poll poll, string type, string title, string body)
    {
        try
        {
            var ids = await engine.EligibleMemberIdsAsync(poll);
            for (var i = 0; i < ids.Count; i += NotifyBatch)
            {
                var batch = ids.Skip(i).Take(NotifyBatch).Select(memberId => new Notification
                {
                    InstitutionId = poll.InstitutionId, RecipientId = memberId, RecipientType = "Member",
                    Title = title, Body = body, Type = type, RelatedEntityId = poll.Id, RelatedEntityType = "Poll",
                    ActionUrl = $"/polls/{poll.Id}", CreatedBy = "system",
                }).ToList();
                await notificationRepo.AddRangeAsync(batch);
            }
        }
        catch (Exception e)
        {
            // The poll itself is already saved; a failed notice must not make the admin think publishing failed.
            logger.LogError(e, "Poll {PollId} was saved but members could not be notified ({Type})", poll.Id, type);
        }
    }

    private static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}

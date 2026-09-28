using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ServiceRequestEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.ServiceRequest;
using StoreOrderEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.StoreOrder;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class ActivationWorkService(
    IAlumniPgRepository<ActivationTarget> targetRepo,
    IAlumniPgRepository<ActivationTask> taskRepo,
    IAlumniPgRepository<ActivationTaskNote> noteRepo,
    IAlumniPgRepository<PlatformStaff> staffRepo,
    IAlumniPgRepository<PlatformNotification> notificationRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<OnboardingLead> leadRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<ContributionEntity> contributionRepo,
    IAlumniPgRepository<StoreOrderEntity> storeOrderRepo,
    IAlumniPgRepository<ServiceRequestEntity> serviceRequestRepo,
    IEmailService emailService,
    IAuditLogService auditLog,
    ILogger<ActivationWorkService> logger) : IActivationWorkService
{
    private static readonly Dictionary<string, string> MetricLabels = new()
    {
        [TargetMetrics.LiveInstitutions] = "Live institutions",
        [TargetMetrics.ActivatedInstitutions] = "Activated institutions",
        [TargetMetrics.TotalMembers] = "Active members",
        [TargetMetrics.OnboardingLeads] = "Onboarding requests received",
        [TargetMetrics.PaymentVolume] = "Payments collected (GHS)",
        [TargetMetrics.Custom] = "Custom",
    };

    private static string NameOf(AuthData a) => $"{a.FirstName} {a.LastName}".Trim();
    private static bool IsSuper(AuthData a) => a.Role == PlatformStaffRoles.SuperAdmin;
    private static DateTime Today => DateTime.UtcNow.Date;
    private static DateTime AsUtcDate(DateTime d) => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);

    // ── Metrics and progress ────────────────────────────────────────────────

    /// <summary>The metric's current reading, straight from platform data. Flow metrics count from <paramref name="since"/>.</summary>
    private async Task<decimal> ReadMetricAsync(string metric, DateTime since, decimal? manual)
    {
        switch (metric)
        {
            case TargetMetrics.LiveInstitutions:
                return await institutionRepo.CountAsync(i => i.Status == "Active");
            case TargetMetrics.ActivatedInstitutions:
                return await institutionRepo.CountAsync(i => i.Status == "Active" && i.ActivatedAt != null);
            case TargetMetrics.TotalMembers:
                return await memberRepo.GetQueryable(m => m.Status == "Active", ignoreQueryFilters: true).CountAsync();
            case TargetMetrics.OnboardingLeads:
                return await leadRepo.CountAsync(l => l.CreatedAt >= since);
            case TargetMetrics.PaymentVolume:
            {
                var c = await contributionRepo.GetQueryable(x => x.Status == "Successful" && (x.ConfirmedAt ?? x.CreatedAt) >= since, ignoreQueryFilters: true).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var o = await storeOrderRepo.GetQueryable(x => x.Status == "Successful" && (x.ConfirmedAt ?? x.CreatedAt) >= since, ignoreQueryFilters: true).SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
                var s = await serviceRequestRepo.GetQueryable(x => x.PaymentStatus == "Successful" && (x.ConfirmedAt ?? x.CreatedAt) >= since, ignoreQueryFilters: true).SumAsync(x => (decimal?)x.Amount) ?? 0;
                return c + o + s;
            }
            default:
                return manual ?? 0;
        }
    }

    private static TargetProgressDto BuildProgress(ActivationTarget t, decimal current)
    {
        var r = ActivationTargetMath.Compute(t, current, Today);
        return new TargetProgressDto(current, r.Baseline, t.GoalValue, r.Expected, r.Percent, r.Health);
    }

    /// <summary>
    /// A target ends the moment its goal is reached (Achieved) or once its due date has passed (Missed). The result is
    /// frozen at that moment so it never changes afterwards, and the owner is told. Idempotent: only Active targets change.
    /// </summary>
    private async Task SyncEndedTargetsAsync(List<ActivationTarget> targets)
    {
        foreach (var t in targets.Where(t => t.Status == TargetStatuses.Active).ToList())
        {
            var current = await ReadMetricAsync(t.Metric, t.StartDate, t.ManualValue);
            string? outcome = current >= t.GoalValue ? TargetStatuses.Achieved : Today > t.DueDate.Date ? TargetStatuses.Missed : null;
            if (outcome is null) continue;

            t.Status = outcome;
            t.FinalValue = current;
            t.ClosedAt = DateTime.UtcNow;
            t.UpdatedAt = DateTime.UtcNow;
            t.UpdatedBy = "system";
            await targetRepo.UpdateAsync(t);
            await NotifyAsync(t.OwnerId,
                outcome == TargetStatuses.Achieved ? "Target achieved" : "Target ended without reaching its goal",
                outcome == TargetStatuses.Achieved
                    ? $"\"{t.Title}\" reached its goal ({FormatValue(t, current)} of {FormatValue(t, t.GoalValue)})."
                    : $"\"{t.Title}\" ended at {FormatValue(t, current)} of {FormatValue(t, t.GoalValue)}. You can start a new target from the Activation page.",
                "TargetClosed", $"/activation/targets/{t.Id}");
        }
    }

    private static string FormatValue(ActivationTarget t, decimal v) =>
        t.Metric == TargetMetrics.PaymentVolume ? $"GHS {v:N2}" : v.ToString("0.##");

    // ── Mapping ─────────────────────────────────────────────────────────────

    private async Task<List<TargetDto>> ToTargetDtosAsync(List<ActivationTarget> targets)
    {
        var ids = targets.Select(t => t.Id).ToList();
        var tasks = ids.Count == 0 ? [] : (await taskRepo.GetAllAsync(t => ids.Contains(t.TargetId))).ToList();
        var result = new List<TargetDto>();
        foreach (var t in targets)
        {
            var current = t.FinalValue ?? await ReadMetricAsync(t.Metric, t.StartDate, t.ManualValue);
            var mine = tasks.Where(x => x.TargetId == t.Id).ToList();
            result.Add(new TargetDto(
                t.Id, t.Title, t.Description, t.Metric, MetricLabels.GetValueOrDefault(t.Metric, t.Metric), TargetMetrics.IsFlow(t.Metric),
                t.GoalValue, t.BaselineValue, t.StartDate, t.DueDate, t.OwnerId, t.OwnerName, t.Status, t.ManualValue, t.ClosedAt,
                BuildProgress(t, current),
                mine.Count(x => x.Status != TaskStatuses.Done),
                mine.Count(x => x.Status == TaskStatuses.Done),
                mine.Count(IsOverdue),
                t.CreatedAt));
        }
        return result;
    }

    private static bool IsOverdue(ActivationTask t) =>
        t.Status != TaskStatuses.Done && t.DueDate.HasValue && Today > t.DueDate.Value.Date;

    private static TaskDto ToTaskDto(ActivationTask t, string targetTitle, AuthData actor)
    {
        var canEdit = IsSuper(actor) || t.CreatedById == actor.Id;
        var canStatus = canEdit || t.AssigneeId == actor.Id;
        return new TaskDto(
            t.Id, t.TargetId, targetTitle, t.Title, t.Description, t.AssigneeId, t.AssigneeName, t.DueDate, t.Priority, t.Status, t.BlockedReason,
            t.InstitutionId, t.InstitutionName, t.LeadId, t.LeadName, t.CreatedById, t.CreatedByName, t.CompletedAt, IsOverdue(t), t.CreatedAt,
            canEdit, canStatus);
    }

    private async Task<Dictionary<string, string>> TargetTitlesAsync(IEnumerable<string> ids)
    {
        var list = ids.Distinct().ToList();
        return list.Count == 0 ? [] : (await targetRepo.GetAllAsync(t => list.Contains(t.Id))).ToDictionary(t => t.Id, t => t.Title);
    }

    // ── Targets ─────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<TargetDto>>> ListTargetsAsync(string? status)
    {
        try
        {
            var targets = (await targetRepo.GetAllAsync(null)).ToList();
            await SyncEndedTargetsAsync(targets);
            var filtered = string.IsNullOrWhiteSpace(status) ? targets : targets.Where(t => t.Status == status).ToList();
            var ordered = filtered.OrderBy(t => t.Status == TargetStatuses.Active ? 0 : 1).ThenBy(t => t.DueDate).ToList();
            return (await ToTargetDtosAsync(ordered)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "ListTargetsAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<TargetDto>>("Failed to load targets");
        }
    }

    public async Task<IApiResponse<TargetDetailDto>> GetTargetAsync(string id, AuthData actor)
    {
        try
        {
            var target = await targetRepo.GetByIdAsync(id);
            if (target is null) return ApiResponseExtensions.ToNotFoundApiResponse<TargetDetailDto>("Target not found");
            await SyncEndedTargetsAsync([target]);
            var dto = (await ToTargetDtosAsync([target]))[0];
            var tasks = (await taskRepo.GetAllAsync(t => t.TargetId == id))
                .OrderBy(t => t.Status == TaskStatuses.Done ? 1 : 0).ThenBy(t => t.DueDate ?? DateTime.MaxValue).ToList();
            return new TargetDetailDto(dto, tasks.Select(t => ToTaskDto(t, target.Title, actor)).ToList()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetTargetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TargetDetailDto>("Failed to load the target");
        }
    }

    private async Task<PlatformStaff?> ActiveStaffAsync(string id) =>
        string.IsNullOrWhiteSpace(id) ? null : await staffRepo.GetOneAsync(s => s.Id == id && !s.IsDisabled);

    public async Task<IApiResponse<TargetDto>> CreateTargetAsync(CreateTargetRequest request, AuthData actor)
    {
        try
        {
            if (!IsSuper(actor)) return ApiResponseExtensions.ToForbiddenApiResponse<TargetDto>("Only a Super Admin can create targets");
            if (!TargetMetrics.All.Contains(request.Metric)) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("Choose what the target measures");
            if (request.GoalValue <= 0) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("The goal must be more than zero");
            var due = AsUtcDate(request.DueDate);
            if (due <= Today) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("The due date must be after today");
            var owner = await ActiveStaffAsync(request.OwnerId);
            if (owner is null) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("Choose an owner from the platform team");

            var start = DateTime.SpecifyKind(Today, DateTimeKind.Utc);
            var baseline = TargetMetrics.IsFlow(request.Metric) ? 0m : await ReadMetricAsync(request.Metric, start, request.ManualValue);
            if (!TargetMetrics.IsFlow(request.Metric) && request.GoalValue <= baseline)
                return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>($"The goal must be above where you are now ({baseline:0.##})");

            var target = new ActivationTarget
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Metric = request.Metric,
                GoalValue = request.GoalValue,
                BaselineValue = baseline,
                StartDate = start,
                DueDate = due,
                OwnerId = owner.Id,
                OwnerName = owner.Name,
                ManualValue = request.Metric == TargetMetrics.Custom ? request.ManualValue ?? 0 : null,
                CreatedBy = actor.Id,
            };
            await targetRepo.AddAsync(target);
            await auditLog.LogAsync(actor.Id, NameOf(actor), "created target", $"{target.Title} (goal {FormatValue(target, target.GoalValue)} by {due:yyyy-MM-dd}, owner {owner.Name})");
            if (owner.Id != actor.Id)
                await NotifyAsync(owner.Id, "You own a new target", $"{NameOf(actor)} made you the owner of \"{target.Title}\": {FormatValue(target, target.GoalValue)} by {due:d MMMM yyyy}.", "TargetAssigned", $"/activation/targets/{target.Id}");
            return (await ToTargetDtosAsync([target]))[0].ToCreatedApiResponse("Target created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "CreateTargetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TargetDto>("Failed to create the target");
        }
    }

    public async Task<IApiResponse<TargetDto>> UpdateTargetAsync(string id, UpdateTargetRequest request, AuthData actor)
    {
        try
        {
            if (!IsSuper(actor)) return ApiResponseExtensions.ToForbiddenApiResponse<TargetDto>("Only a Super Admin can change targets");
            var target = await targetRepo.GetByIdAsync(id);
            if (target is null) return ApiResponseExtensions.ToNotFoundApiResponse<TargetDto>("Target not found");
            if (target.Status != TargetStatuses.Active) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("This target has ended. Create a new one instead.");
            if (request.GoalValue <= 0) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("The goal must be more than zero");
            var due = AsUtcDate(request.DueDate);
            if (due <= target.StartDate.Date) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("The due date must be after the start date");
            var owner = await ActiveStaffAsync(request.OwnerId);
            if (owner is null) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("Choose an owner from the platform team");

            var changes = new List<string>();
            if (target.Title != request.Title.Trim()) changes.Add("title");
            if (target.GoalValue != request.GoalValue) changes.Add($"goal {FormatValue(target, target.GoalValue)} → {FormatValue(target, request.GoalValue)}");
            if (target.DueDate.Date != due) changes.Add($"due {target.DueDate:yyyy-MM-dd} → {due:yyyy-MM-dd}");
            var ownerChanged = target.OwnerId != owner.Id;
            if (ownerChanged) changes.Add($"owner {target.OwnerName} → {owner.Name}");

            target.Title = request.Title.Trim();
            target.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            target.GoalValue = request.GoalValue;
            target.DueDate = due;
            target.OwnerId = owner.Id;
            target.OwnerName = owner.Name;
            if (target.Metric == TargetMetrics.Custom && request.ManualValue.HasValue) target.ManualValue = request.ManualValue;
            target.UpdatedAt = DateTime.UtcNow;
            target.UpdatedBy = actor.Id;
            await targetRepo.UpdateAsync(target);
            if (changes.Count > 0) await auditLog.LogAsync(actor.Id, NameOf(actor), "updated target", $"{target.Title}: {string.Join("; ", changes)}");
            if (ownerChanged && owner.Id != actor.Id)
                await NotifyAsync(owner.Id, "You now own a target", $"{NameOf(actor)} made you the owner of \"{target.Title}\".", "TargetAssigned", $"/activation/targets/{target.Id}");
            await SyncEndedTargetsAsync([target]);
            return (await ToTargetDtosAsync([target]))[0].ToOkApiResponse("Target updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateTargetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TargetDto>("Failed to update the target");
        }
    }

    public async Task<IApiResponse<TargetDto>> CancelTargetAsync(string id, AuthData actor)
    {
        try
        {
            if (!IsSuper(actor)) return ApiResponseExtensions.ToForbiddenApiResponse<TargetDto>("Only a Super Admin can cancel targets");
            var target = await targetRepo.GetByIdAsync(id);
            if (target is null) return ApiResponseExtensions.ToNotFoundApiResponse<TargetDto>("Target not found");
            if (target.Status != TargetStatuses.Active) return ApiResponseExtensions.ToBadRequestApiResponse<TargetDto>("This target has already ended");

            target.FinalValue = await ReadMetricAsync(target.Metric, target.StartDate, target.ManualValue);
            target.Status = TargetStatuses.Cancelled;
            target.ClosedAt = DateTime.UtcNow;
            target.UpdatedAt = DateTime.UtcNow;
            target.UpdatedBy = actor.Id;
            await targetRepo.UpdateAsync(target);
            await auditLog.LogAsync(actor.Id, NameOf(actor), "cancelled target", target.Title);
            return (await ToTargetDtosAsync([target]))[0].ToOkApiResponse("Target cancelled");
        }
        catch (Exception e)
        {
            logger.LogError(e, "CancelTargetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TargetDto>("Failed to cancel the target");
        }
    }

    // ── Tasks ───────────────────────────────────────────────────────────────

    public async Task<IApiResponse<List<TaskDto>>> ListTasksAsync(AuthData actor, bool mine, string? targetId, string? status, string? assigneeId, string? institutionId, string? leadId)
    {
        try
        {
            var assignee = mine ? actor.Id : assigneeId;
            var tasks = (await taskRepo.GetAllAsync(t =>
                    (targetId == null || t.TargetId == targetId)
                    && (assignee == null || t.AssigneeId == assignee)
                    && (institutionId == null || t.InstitutionId == institutionId)
                    && (leadId == null || t.LeadId == leadId)
                    && (status == null || t.Status == status)))
                .OrderBy(t => t.Status == TaskStatuses.Done ? 1 : 0).ThenBy(t => t.DueDate ?? DateTime.MaxValue).ThenByDescending(t => t.CreatedAt).ToList();
            var titles = await TargetTitlesAsync(tasks.Select(t => t.TargetId));
            return tasks.Select(t => ToTaskDto(t, titles.GetValueOrDefault(t.TargetId, "Target"), actor)).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "ListTasksAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<TaskDto>>("Failed to load tasks");
        }
    }

    public async Task<IApiResponse<TaskDetailDto>> GetTaskAsync(string id, AuthData actor)
    {
        try
        {
            var task = await taskRepo.GetByIdAsync(id);
            if (task is null) return ApiResponseExtensions.ToNotFoundApiResponse<TaskDetailDto>("Task not found");
            var titles = await TargetTitlesAsync([task.TargetId]);
            var notes = (await noteRepo.GetAllAsync(n => n.TaskId == id)).OrderBy(n => n.CreatedAt)
                .Select(n => new TaskNoteDto(n.Id, n.AuthorId, n.AuthorName, n.Text, n.CreatedAt)).ToList();
            return new TaskDetailDto(ToTaskDto(task, titles.GetValueOrDefault(task.TargetId, "Target"), actor), notes).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetTaskAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TaskDetailDto>("Failed to load the task");
        }
    }

    /// <summary>Resolves the institution / lead a task is about into display names, or an error message.</summary>
    private async Task<(string? InstitutionName, string? LeadName, string? Error)> ResolveLinksAsync(string? institutionId, string? leadId)
    {
        string? institutionName = null, leadName = null;
        if (!string.IsNullOrWhiteSpace(institutionId))
        {
            var inst = await institutionRepo.GetByIdAsync(institutionId);
            if (inst is null) return (null, null, "That institution no longer exists");
            institutionName = inst.Name;
        }
        if (!string.IsNullOrWhiteSpace(leadId))
        {
            var lead = await leadRepo.GetByIdAsync(leadId);
            if (lead is null) return (null, null, "That onboarding request no longer exists");
            leadName = lead.InstitutionName;
        }
        return (institutionName, leadName, null);
    }

    public async Task<IApiResponse<TaskDto>> CreateTaskAsync(CreateTaskRequest request, AuthData actor)
    {
        try
        {
            var target = await targetRepo.GetByIdAsync(request.TargetId);
            if (target is null) return ApiResponseExtensions.ToNotFoundApiResponse<TaskDto>("Target not found");
            if (target.Status != TargetStatuses.Active) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("This target has ended, so it can't take new tasks");

            var assigneeId = string.IsNullOrWhiteSpace(request.AssigneeId) ? actor.Id : request.AssigneeId;
            if (assigneeId != actor.Id && !IsSuper(actor))
                return ApiResponseExtensions.ToForbiddenApiResponse<TaskDto>("Only a Super Admin can assign tasks to other people");
            var assignee = await ActiveStaffAsync(assigneeId);
            if (assignee is null) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("Choose someone from the platform team");
            var priority = string.IsNullOrWhiteSpace(request.Priority) ? TaskPriorities.Normal : request.Priority;
            if (!TaskPriorities.All.Contains(priority)) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("Unknown priority");
            var (instName, leadName, error) = await ResolveLinksAsync(request.InstitutionId, request.LeadId);
            if (error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>(error);

            var task = new ActivationTask
            {
                TargetId = target.Id,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                AssigneeId = assignee.Id,
                AssigneeName = assignee.Name,
                DueDate = request.DueDate.HasValue ? AsUtcDate(request.DueDate.Value) : null,
                Priority = priority,
                InstitutionId = string.IsNullOrWhiteSpace(request.InstitutionId) ? null : request.InstitutionId,
                InstitutionName = instName,
                LeadId = string.IsNullOrWhiteSpace(request.LeadId) ? null : request.LeadId,
                LeadName = leadName,
                CreatedById = actor.Id,
                CreatedByName = NameOf(actor),
                CreatedBy = actor.Id,
            };
            await taskRepo.AddAsync(task);
            await auditLog.LogAsync(actor.Id, NameOf(actor), "created task", $"{task.Title} → {assignee.Name} (target: {target.Title})");
            if (assignee.Id != actor.Id)
                await NotifyAsync(assignee.Id, "New task for you", $"{NameOf(actor)} assigned you \"{task.Title}\"{DueText(task)}, under \"{target.Title}\".", "TaskAssigned", $"/activation?tab=tasks");
            return ToTaskDto(task, target.Title, actor).ToCreatedApiResponse("Task created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "CreateTaskAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TaskDto>("Failed to create the task");
        }
    }

    private static string DueText(ActivationTask t) => t.DueDate.HasValue ? $", due {t.DueDate.Value:d MMMM}" : "";

    public async Task<IApiResponse<TaskDto>> UpdateTaskAsync(string id, UpdateTaskRequest request, AuthData actor)
    {
        try
        {
            var task = await taskRepo.GetByIdAsync(id);
            if (task is null) return ApiResponseExtensions.ToNotFoundApiResponse<TaskDto>("Task not found");
            if (!(IsSuper(actor) || task.CreatedById == actor.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<TaskDto>("Only the person who created this task, or a Super Admin, can change its details");
            var target = await targetRepo.GetByIdAsync(task.TargetId);
            if (target is not null && target.Status != TargetStatuses.Active)
                return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("This target has ended, so its tasks are read-only");

            var assigneeId = string.IsNullOrWhiteSpace(request.AssigneeId) ? task.AssigneeId : request.AssigneeId;
            var reassigned = assigneeId != task.AssigneeId;
            if (reassigned && !IsSuper(actor)) return ApiResponseExtensions.ToForbiddenApiResponse<TaskDto>("Only a Super Admin can assign tasks to other people");
            var assignee = reassigned ? await ActiveStaffAsync(assigneeId) : null;
            if (reassigned && assignee is null) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("Choose someone from the platform team");
            var priority = string.IsNullOrWhiteSpace(request.Priority) ? task.Priority : request.Priority;
            if (!TaskPriorities.All.Contains(priority)) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("Unknown priority");
            var (instName, leadName, error) = await ResolveLinksAsync(request.InstitutionId, request.LeadId);
            if (error is not null) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>(error);

            var newDue = request.DueDate.HasValue ? AsUtcDate(request.DueDate.Value) : (DateTime?)null;
            if (newDue != task.DueDate?.Date && !(newDue is null && task.DueDate is null))
            {
                // A new date deserves fresh reminders.
                task.DueSoonNotifiedAt = task.OverdueNotifiedAt = task.LastReminderAt = null;
            }
            task.Title = request.Title.Trim();
            task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            task.DueDate = newDue;
            task.Priority = priority;
            task.InstitutionId = string.IsNullOrWhiteSpace(request.InstitutionId) ? null : request.InstitutionId;
            task.InstitutionName = instName;
            task.LeadId = string.IsNullOrWhiteSpace(request.LeadId) ? null : request.LeadId;
            task.LeadName = leadName;
            if (reassigned)
            {
                task.AssigneeId = assignee!.Id;
                task.AssigneeName = assignee.Name;
                task.DueSoonNotifiedAt = task.OverdueNotifiedAt = task.LastReminderAt = null;
            }
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = actor.Id;
            await taskRepo.UpdateAsync(task);
            await auditLog.LogAsync(actor.Id, NameOf(actor), "updated task", reassigned ? $"{task.Title} → reassigned to {task.AssigneeName}" : task.Title);
            if (reassigned && task.AssigneeId != actor.Id)
                await NotifyAsync(task.AssigneeId, "A task was assigned to you", $"{NameOf(actor)} assigned you \"{task.Title}\"{DueText(task)}.", "TaskAssigned", "/activation?tab=tasks");
            return ToTaskDto(task, target?.Title ?? "Target", actor).ToOkApiResponse("Task updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateTaskAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TaskDto>("Failed to update the task");
        }
    }

    public async Task<IApiResponse<TaskDto>> UpdateTaskStatusAsync(string id, UpdateTaskStatusRequest request, AuthData actor)
    {
        try
        {
            var task = await taskRepo.GetByIdAsync(id);
            if (task is null) return ApiResponseExtensions.ToNotFoundApiResponse<TaskDto>("Task not found");
            if (!(IsSuper(actor) || task.CreatedById == actor.Id || task.AssigneeId == actor.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<TaskDto>("Only the assignee, the creator or a Super Admin can update this task");
            if (!TaskStatuses.All.Contains(request.Status)) return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("Unknown status");
            var blocked = request.Status == TaskStatuses.Blocked;
            if (blocked && string.IsNullOrWhiteSpace(request.BlockedReason))
                return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("Say what is blocking this task");
            var target = await targetRepo.GetByIdAsync(task.TargetId);
            if (target is not null && target.Status != TargetStatuses.Active)
                return ApiResponseExtensions.ToBadRequestApiResponse<TaskDto>("This target has ended, so its tasks are read-only");

            var was = task.Status;
            task.Status = request.Status;
            task.BlockedReason = blocked ? request.BlockedReason!.Trim() : null;
            task.CompletedAt = request.Status == TaskStatuses.Done ? task.CompletedAt ?? DateTime.UtcNow : null;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = actor.Id;
            await taskRepo.UpdateAsync(task);
            if (was != task.Status) await auditLog.LogAsync(actor.Id, NameOf(actor), "updated task status", $"{task.Title}: {was} → {task.Status}");
            // Tell the person who set the task when someone else finishes or blocks it.
            if (was != task.Status && task.CreatedById != actor.Id && task.Status is TaskStatuses.Done or TaskStatuses.Blocked)
                await NotifyAsync(task.CreatedById, task.Status == TaskStatuses.Done ? "Task completed" : "Task blocked",
                    task.Status == TaskStatuses.Done ? $"{NameOf(actor)} completed \"{task.Title}\"." : $"{NameOf(actor)} marked \"{task.Title}\" as blocked: {task.BlockedReason}",
                    "TaskUpdated", "/activation?tab=tasks", email: false);
            return ToTaskDto(task, target?.Title ?? "Target", actor).ToOkApiResponse("Task updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "UpdateTaskStatusAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TaskDto>("Failed to update the task");
        }
    }

    public async Task<IApiResponse<object>> DeleteTaskAsync(string id, AuthData actor)
    {
        try
        {
            var task = await taskRepo.GetByIdAsync(id);
            if (task is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("Task not found");
            if (!(IsSuper(actor) || task.CreatedById == actor.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("Only the person who created this task, or a Super Admin, can delete it");
            foreach (var n in await noteRepo.GetAllAsync(n => n.TaskId == id)) await noteRepo.RemoveAsync(n);
            await taskRepo.RemoveAsync(task);
            await auditLog.LogAsync(actor.Id, NameOf(actor), "deleted task", task.Title);
            return new object().ToOkApiResponse("Task deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "DeleteTaskAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete the task");
        }
    }

    public async Task<IApiResponse<TaskNoteDto>> AddNoteAsync(string taskId, AddTaskNoteRequest request, AuthData actor)
    {
        try
        {
            var task = await taskRepo.GetByIdAsync(taskId);
            if (task is null) return ApiResponseExtensions.ToNotFoundApiResponse<TaskNoteDto>("Task not found");
            if (!(IsSuper(actor) || task.CreatedById == actor.Id || task.AssigneeId == actor.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<TaskNoteDto>("Only the assignee, the creator or a Super Admin can add notes");
            var text = request.Text.Trim();
            if (text.Length == 0) return ApiResponseExtensions.ToBadRequestApiResponse<TaskNoteDto>("Write a note first");
            var note = new ActivationTaskNote { TaskId = taskId, AuthorId = actor.Id, AuthorName = NameOf(actor), Text = text, CreatedBy = actor.Id };
            await noteRepo.AddAsync(note);
            return new TaskNoteDto(note.Id, note.AuthorId, note.AuthorName, note.Text, note.CreatedAt).ToCreatedApiResponse("Note added");
        }
        catch (Exception e)
        {
            logger.LogError(e, "AddNoteAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<TaskNoteDto>("Failed to add the note");
        }
    }

    // ── Notifications ───────────────────────────────────────────────────────

    /// <summary>An in-app notice for a platform staff member, plus an email unless <paramref name="email"/> is false. Never fails the caller.</summary>
    private async Task NotifyAsync(string staffId, string title, string body, string type, string actionUrl, bool email = true)
    {
        try
        {
            var staff = await staffRepo.GetOneAsync(s => s.Id == staffId && !s.IsDisabled);
            if (staff is null) return;
            await notificationRepo.AddAsync(new PlatformNotification
            {
                RecipientStaffId = staff.Id, Title = title, Body = body, Type = type, ActionUrl = actionUrl, CreatedBy = "system",
            });
            if (!email || string.IsNullOrWhiteSpace(staff.Email)) return;
            await emailService.SendEmailAsync(new SendEmailRequest
            {
                To = [new EmailContact { Email = staff.Email, Name = staff.Name }],
                TemplateId = "notification",
                TemplateVariables = new
                {
                    first_name = staff.Name.Split(' ')[0],
                    title,
                    body,
                    badge_label = "Platform team",
                    pref_label = "you are on the AlumUnion platform team",
                },
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to notify platform staff {StaffId}: {Title}", staffId, title);
        }
    }
}

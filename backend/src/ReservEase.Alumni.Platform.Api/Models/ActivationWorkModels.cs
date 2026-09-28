using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>How far a target has got. Health is Achieved, OnTrack, Behind, Missed or Cancelled.</summary>
public record TargetProgressDto(decimal Current, decimal Baseline, decimal Goal, decimal Expected, int Percent, string Health);

public record TargetDto(
    string Id, string Title, string? Description, string Metric, string MetricLabel, bool IsFlow,
    decimal GoalValue, decimal BaselineValue, DateTime StartDate, DateTime DueDate,
    string OwnerId, string OwnerName, string Status, decimal? ManualValue, DateTime? ClosedAt,
    TargetProgressDto Progress, int OpenTasks, int DoneTasks, int OverdueTasks, DateTime CreatedAt);

public record TaskDto(
    string Id, string TargetId, string TargetTitle, string Title, string? Description,
    string AssigneeId, string AssigneeName, DateTime? DueDate, string Priority, string Status, string? BlockedReason,
    string? InstitutionId, string? InstitutionName, string? LeadId, string? LeadName,
    string CreatedById, string CreatedByName, DateTime? CompletedAt, bool IsOverdue, DateTime CreatedAt,
    /// <summary>The caller may edit the task's details.</summary>
    bool CanEdit,
    /// <summary>The caller may move it between statuses and add notes.</summary>
    bool CanUpdateStatus);

public record TaskNoteDto(string Id, string AuthorId, string AuthorName, string Text, DateTime CreatedAt);

public record TaskDetailDto(TaskDto Task, List<TaskNoteDto> Notes);

public record TargetDetailDto(TargetDto Target, List<TaskDto> Tasks);

public class CreateTargetRequest
{
    [Required, MaxLength(160)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [Required] public string Metric { get; set; } = string.Empty;
    public decimal GoalValue { get; set; }
    public DateTime DueDate { get; set; }
    [Required] public string OwnerId { get; set; } = string.Empty;
    /// <summary>Starting figure for a Custom target.</summary>
    public decimal? ManualValue { get; set; }
}

public class UpdateTargetRequest
{
    [Required, MaxLength(160)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public decimal GoalValue { get; set; }
    public DateTime DueDate { get; set; }
    [Required] public string OwnerId { get; set; } = string.Empty;
    /// <summary>New figure for a Custom target.</summary>
    public decimal? ManualValue { get; set; }
}

public class CreateTaskRequest
{
    [Required] public string TargetId { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(4000)] public string? Description { get; set; }
    /// <summary>Only a Super Admin may assign to someone else; everyone else's tasks are assigned to themselves.</summary>
    public string? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }
    public string? InstitutionId { get; set; }
    public string? LeadId { get; set; }
}

public class UpdateTaskRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(4000)] public string? Description { get; set; }
    public string? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }
    public string? InstitutionId { get; set; }
    public string? LeadId { get; set; }
}

public class UpdateTaskStatusRequest
{
    [Required] public string Status { get; set; } = string.Empty;
    [MaxLength(500)] public string? BlockedReason { get; set; }
}

public class AddTaskNoteRequest
{
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
}

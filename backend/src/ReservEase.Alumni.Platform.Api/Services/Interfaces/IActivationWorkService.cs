using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

/// <summary>Targets the platform team is working towards, and the tasks under them.</summary>
public interface IActivationWorkService
{
    Task<IApiResponse<List<TargetDto>>> ListTargetsAsync(string? status);
    Task<IApiResponse<TargetDetailDto>> GetTargetAsync(string id, AuthData actor);
    Task<IApiResponse<TargetDto>> CreateTargetAsync(CreateTargetRequest request, AuthData actor);
    Task<IApiResponse<TargetDto>> UpdateTargetAsync(string id, UpdateTargetRequest request, AuthData actor);
    Task<IApiResponse<TargetDto>> CancelTargetAsync(string id, AuthData actor);

    Task<IApiResponse<List<TaskDto>>> ListTasksAsync(AuthData actor, bool mine, string? targetId, string? status, string? assigneeId, string? institutionId, string? leadId);
    Task<IApiResponse<TaskDetailDto>> GetTaskAsync(string id, AuthData actor);
    Task<IApiResponse<TaskDto>> CreateTaskAsync(CreateTaskRequest request, AuthData actor);
    Task<IApiResponse<TaskDto>> UpdateTaskAsync(string id, UpdateTaskRequest request, AuthData actor);
    Task<IApiResponse<TaskDto>> UpdateTaskStatusAsync(string id, UpdateTaskStatusRequest request, AuthData actor);
    Task<IApiResponse<object>> DeleteTaskAsync(string id, AuthData actor);
    Task<IApiResponse<TaskNoteDto>> AddNoteAsync(string taskId, AddTaskNoteRequest request, AuthData actor);
}

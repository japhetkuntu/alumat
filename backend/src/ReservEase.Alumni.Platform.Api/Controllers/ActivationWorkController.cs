using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// Targets the platform team is working towards and the tasks under them. Every platform staff member can read them and
/// work their own tasks; what each person may change is decided in the service (Super Admins create targets and assign work).
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/work")]
public class ActivationWorkController(IActivationWorkService workService) : DefaultController
{
    [HttpGet("targets")]
    [SwaggerOperation(Summary = "List targets with their progress")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<TargetDto>>))]
    public async Task<IActionResult> ListTargets([FromQuery] string? status = null) =>
        (await workService.ListTargetsAsync(status)).ToActionResult();

    [HttpGet("targets/{id}")]
    [SwaggerOperation(Summary = "One target with its tasks")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TargetDetailDto>))]
    public async Task<IActionResult> GetTarget(string id) =>
        (await workService.GetTargetAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("targets")]
    [SwaggerOperation(Summary = "Create a target (Super Admin)")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<TargetDto>))]
    public async Task<IActionResult> CreateTarget([FromBody] CreateTargetRequest request) =>
        (await workService.CreateTargetAsync(request, User.GetAccount())).ToActionResult();

    [HttpPut("targets/{id}")]
    [SwaggerOperation(Summary = "Change a target (Super Admin)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TargetDto>))]
    public async Task<IActionResult> UpdateTarget(string id, [FromBody] UpdateTargetRequest request) =>
        (await workService.UpdateTargetAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpPost("targets/{id}/cancel")]
    [SwaggerOperation(Summary = "Cancel a target (Super Admin)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TargetDto>))]
    public async Task<IActionResult> CancelTarget(string id) =>
        (await workService.CancelTargetAsync(id, User.GetAccount())).ToActionResult();

    [HttpGet("tasks")]
    [SwaggerOperation(Summary = "List tasks", Description = "mine=true for the caller's own tasks; or filter by target, status, assignee, institution or onboarding request.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<TaskDto>>))]
    public async Task<IActionResult> ListTasks([FromQuery] bool mine = false, [FromQuery] string? targetId = null, [FromQuery] string? status = null,
        [FromQuery] string? assigneeId = null, [FromQuery] string? institutionId = null, [FromQuery] string? leadId = null) =>
        (await workService.ListTasksAsync(User.GetAccount(), mine, targetId, status, assigneeId, institutionId, leadId)).ToActionResult();

    [HttpGet("tasks/{id}")]
    [SwaggerOperation(Summary = "One task with its notes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TaskDetailDto>))]
    public async Task<IActionResult> GetTask(string id) =>
        (await workService.GetTaskAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("tasks")]
    [SwaggerOperation(Summary = "Create a task under a target")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<TaskDto>))]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request) =>
        (await workService.CreateTaskAsync(request, User.GetAccount())).ToActionResult();

    [HttpPut("tasks/{id}")]
    [SwaggerOperation(Summary = "Change a task's details")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TaskDto>))]
    public async Task<IActionResult> UpdateTask(string id, [FromBody] UpdateTaskRequest request) =>
        (await workService.UpdateTaskAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpPatch("tasks/{id}/status")]
    [SwaggerOperation(Summary = "Move a task between statuses")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<TaskDto>))]
    public async Task<IActionResult> UpdateTaskStatus(string id, [FromBody] UpdateTaskStatusRequest request) =>
        (await workService.UpdateTaskStatusAsync(id, request, User.GetAccount())).ToActionResult();

    [HttpDelete("tasks/{id}")]
    [SwaggerOperation(Summary = "Delete a task")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteTask(string id) =>
        (await workService.DeleteTaskAsync(id, User.GetAccount())).ToActionResult();

    [HttpPost("tasks/{id}/notes")]
    [SwaggerOperation(Summary = "Add a note to a task")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<TaskNoteDto>))]
    public async Task<IActionResult> AddNote(string id, [FromBody] AddTaskNoteRequest request) =>
        (await workService.AddNoteAsync(id, request, User.GetAccount())).ToActionResult();
}

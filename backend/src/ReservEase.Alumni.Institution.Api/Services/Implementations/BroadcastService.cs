using System.Linq.Expressions;
using ReservEase.Alumni.Institution.Api.Actors;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class BroadcastService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    INotificationActor notificationActor,
    ICurrentTenantService currentTenant,
    ILogger<BroadcastService> logger) : IBroadcastService
{
    private static Expression<Func<MemberEntity, bool>> BuildPredicate(BroadcastFilter filter, AuthData admin)
    {
        var isSuper = admin.Role != StaffRoles.ScopedAdmin;
        var yearGroups = admin.YearGroups ?? new List<int>();

        return m => (isSuper || yearGroups.Contains(m.GraduationYear))
            && (string.IsNullOrEmpty(filter.Status) || m.Status == filter.Status)
            && (string.IsNullOrEmpty(filter.DepartmentId) || m.DepartmentId == filter.DepartmentId)
            && (!filter.GraduationYearFrom.HasValue || m.GraduationYear >= filter.GraduationYearFrom.Value)
            && (!filter.GraduationYearTo.HasValue || m.GraduationYear <= filter.GraduationYearTo.Value);
    }

    public async Task<IApiResponse<int>> GetRecipientCountAsync(BroadcastFilter filter, AuthData admin)
    {
        try
        {
            var count = await memberRepo.CountAsync(BuildPredicate(filter, admin));
            return count.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error counting broadcast recipients with filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<int>("Failed to count recipients");
        }
    }

    public async Task<IApiResponse<BroadcastResult>> SendBroadcastAsync(SendBroadcastRequest request, AuthData admin)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponseExtensions.ToBadRequestApiResponse<BroadcastResult>("Message is required");

            var channels = request.Channels.Count == 0 ? new List<string> { "InApp" } : request.Channels;

            var members = await memberRepo.GetAllAsync(BuildPredicate(request.Filter, admin));
            var recipients = members
                .Select(m => new BroadcastRecipient(m.Id, m.Email, m.FirstName, m.Phone))
                .ToList();

            if (recipients.Count > 0)
            {
                notificationActor.Tell(new SendBroadcastCommand(currentTenant.InstitutionId!, recipients, request.Title, request.Message, channels));
            }

            logger.LogInformation("Broadcast queued by admin {AdminId} to {Count} recipients via [{Channels}]",
                admin.Id, recipients.Count, string.Join(",", channels));

            return new BroadcastResult(recipients.Count, channels).ToOkApiResponse("Broadcast queued");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending broadcast by admin {AdminId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BroadcastResult>("Failed to send broadcast");
        }
    }
}

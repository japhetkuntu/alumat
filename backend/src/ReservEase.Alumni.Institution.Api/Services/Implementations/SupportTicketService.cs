using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// SupportCase is a global (not tenant-scoped) entity — the EF tenant query
/// filter does not apply to it, so every query here must filter by
/// currentTenant.InstitutionId explicitly to keep one institution from
/// seeing another's tickets.
/// </summary>
public class SupportTicketService(
    IAlumniPgRepository<SupportCase> supportRepo,
    ICurrentTenantService currentTenant,
    ILogger<SupportTicketService> logger) : ISupportTicketService
{
    public async Task<IApiResponse<List<SupportTicketResponse>>> GetTicketsAsync(AuthData admin)
    {
        try
        {
            var tickets = await supportRepo.GetAllAsync(c => c.InstitutionId == currentTenant.InstitutionId);
            var dtos = tickets
                .OrderByDescending(c => c.CreatedAt)
                .Select(ToDto)
                .ToList();
            return dtos.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving support tickets for institution {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<SupportTicketResponse>>("Failed to retrieve support tickets");
        }
    }

    public async Task<IApiResponse<SupportTicketResponse>> CreateTicketAsync(CreateSupportTicketRequest request, AuthData admin)
    {
        try
        {
            var ticket = new SupportCase
            {
                InstitutionId = currentTenant.InstitutionId,
                Subject = request.Subject,
                Severity = request.Severity,
                Requester = admin.Name,
                RequesterEmail = admin.Email,
                Message = request.Message,
                CreatedBy = admin.Id,
            };
            await supportRepo.AddAsync(ticket);
            logger.LogInformation("Support ticket {TicketId} created by admin {AdminId} for institution {InstitutionId}", ticket.Id, admin.Id, currentTenant.InstitutionId);
            return ToDto(ticket).ToCreatedApiResponse("Support ticket submitted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating support ticket for institution {InstitutionId}", currentTenant.InstitutionId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SupportTicketResponse>("Failed to submit support ticket");
        }
    }

    private static SupportTicketResponse ToDto(SupportCase c) =>
        new(c.Id, c.Subject, c.Severity, c.Status, c.Message, c.InternalNote, c.CreatedAt, c.UpdatedAt);
}

using System.Text.RegularExpressions;
using ReservEase.Alumni.Institution.Api.Actors;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// Institution-side management of the "Alumni Services" catalog (transcripts,
/// attestation letters, certificate reissue, etc.) and the queue of member
/// requests against it. Same SuperAdmin-only, self-contained-checkout shape
/// as Store — see StoreService's own doc comment for the platform-fee
/// rationale, which ServiceRequestService mirrors on the Member.Api side.
/// </summary>
public class ServiceService(
    IAlumniPgRepository<ServiceType> serviceTypeRepo,
    IAlumniPgRepository<ServiceRequest> requestRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IStorageService storageService,
    INotificationActor notificationActor,
    ICurrentTenantService currentTenant,
    ILogger<ServiceService> logger) : IServiceService
{
    private const string AttachmentFolderName = "alumni";

    private static string Slugify(string label)
    {
        var slug = Regex.Replace(label.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }

    private static List<ServiceFieldDefinition> MapFields(List<ServiceFieldDefinitionRequest> requests)
    {
        var usedKeys = new HashSet<string>();
        var fields = new List<ServiceFieldDefinition>();
        foreach (var r in requests)
        {
            var key = string.IsNullOrWhiteSpace(r.Key) ? Slugify(r.Label) : r.Key.Trim();
            // Guard against two fields colliding on the same key (e.g. two fields both labeled "Reason").
            var candidate = key;
            var suffix = 1;
            while (!usedKeys.Add(candidate))
                candidate = $"{key}_{++suffix}";

            fields.Add(new ServiceFieldDefinition
            {
                Key = candidate,
                Label = r.Label.Trim(),
                Type = r.Type,
                Required = r.Required,
                Options = r.Options?.Select(o => o.Trim()).Where(o => o.Length > 0).ToList(),
                HelpText = string.IsNullOrWhiteSpace(r.HelpText) ? null : r.HelpText.Trim(),
            });
        }
        return fields;
    }

    private static List<string> NormalizeStages(List<string> stages)
    {
        var cleaned = stages.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return cleaned.Count > 0 ? cleaned : ["Submitted"];
    }

    public async Task<IApiResponse<PgPagedResult<ServiceTypeDto>>> GetServiceTypesAsync(ServiceTypeFilter filter)
    {
        try
        {
            var search = filter.Search?.ToLower();
            var result = await serviceTypeRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                s => (string.IsNullOrEmpty(filter.Status) || s.Status == filter.Status)
                  && (string.IsNullOrEmpty(search) || s.Name.ToLower().Contains(search)));

            var dtoResult = new PgPagedResult<ServiceTypeDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(s => s.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse("Service types retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving service types");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ServiceTypeDto>>("Failed to retrieve service types");
        }
    }

    public async Task<IApiResponse<ServiceTypeDto>> GetServiceTypeByIdAsync(string serviceTypeId)
    {
        try
        {
            var serviceType = await serviceTypeRepo.GetByIdAsync(serviceTypeId);
            if (serviceType is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ServiceTypeDto>("Service type not found");
            return serviceType.ToDto().ToOkApiResponse("Service type retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving service type {ServiceTypeId}", serviceTypeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceTypeDto>("Failed to retrieve service type");
        }
    }

    public async Task<IApiResponse<ServiceTypeDto>> CreateServiceTypeAsync(CreateServiceTypeRequest request, AuthData admin)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceTypeDto>("Name is required");
            if (request.Price <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceTypeDto>("Price must be greater than zero — services can't be free");

            var serviceType = new ServiceType
            {
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Price = request.Price,
                Status = request.Status,
                Fields = MapFields(request.Fields),
                Stages = NormalizeStages(request.Stages),
                CreatedBy = admin.Id,
            };

            await serviceTypeRepo.AddAsync(serviceType);
            logger.LogInformation("Service type {ServiceTypeId} \"{Name}\" created by admin {AdminId}", serviceType.Id, serviceType.Name, admin.Id);
            return serviceType.ToDto().ToOkApiResponse("Service type created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating service type for admin {AdminId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceTypeDto>("Failed to create service type");
        }
    }

    public async Task<IApiResponse<ServiceTypeDto>> UpdateServiceTypeAsync(UpdateServiceTypeRequest request, AuthData admin)
    {
        try
        {
            var serviceType = await serviceTypeRepo.GetByIdAsync(request.ServiceTypeId);
            if (serviceType is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ServiceTypeDto>("Service type not found");
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceTypeDto>("Name is required");
            if (request.Price <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceTypeDto>("Price must be greater than zero — services can't be free");

            serviceType.Name = request.Name.Trim();
            serviceType.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            serviceType.Price = request.Price;
            serviceType.Status = request.Status;
            serviceType.Fields = MapFields(request.Fields);
            serviceType.Stages = NormalizeStages(request.Stages);
            serviceType.UpdatedAt = DateTime.UtcNow;
            serviceType.UpdatedBy = admin.Id;

            await serviceTypeRepo.UpdateAsync(serviceType);
            logger.LogInformation("Service type {ServiceTypeId} updated by admin {AdminId}", serviceType.Id, admin.Id);
            return serviceType.ToDto().ToOkApiResponse("Service type updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating service type {ServiceTypeId}", request.ServiceTypeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceTypeDto>("Failed to update service type");
        }
    }

    public async Task<IApiResponse<object>> DeleteServiceTypeAsync(string serviceTypeId)
    {
        try
        {
            var serviceType = await serviceTypeRepo.GetByIdAsync(serviceTypeId);
            if (serviceType is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Service type not found");

            var hasRequests = (await requestRepo.GetAllAsync(r => r.ServiceTypeId == serviceTypeId)).Any();
            if (hasRequests)
            {
                // Requests snapshot the service's name/price, so archiving (not deleting) keeps
                // their history intact while removing the type from the member-facing catalog.
                serviceType.Status = "Archived";
                serviceType.UpdatedAt = DateTime.UtcNow;
                await serviceTypeRepo.UpdateAsync(serviceType);
                return new object().ToOkApiResponse("Service type has existing requests, so it was archived instead of deleted");
            }

            await serviceTypeRepo.RemoveAsync(serviceType);
            return new object().ToOkApiResponse("Service type deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting service type {ServiceTypeId}", serviceTypeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete service type");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ServiceRequestDto>>> GetRequestsAsync(ServiceRequestFilter filter)
    {
        try
        {
            var result = await requestRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => (string.IsNullOrEmpty(filter.ServiceTypeId) || r.ServiceTypeId == filter.ServiceTypeId)
                  && (string.IsNullOrEmpty(filter.Stage) || r.CurrentStage == filter.Stage)
                  && (string.IsNullOrEmpty(filter.PaymentStatus) || r.PaymentStatus == filter.PaymentStatus));

            var dtoResult = new PgPagedResult<ServiceRequestDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(r => r.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse("Requests retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving service requests");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ServiceRequestDto>>("Failed to retrieve requests");
        }
    }

    public async Task<IApiResponse<ServiceRequestDto>> GetRequestByIdAsync(string requestId)
    {
        try
        {
            var request = await requestRepo.GetByIdAsync(requestId);
            if (request is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ServiceRequestDto>("Request not found");
            return request.ToDto().ToOkApiResponse("Request retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving service request {RequestId}", requestId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceRequestDto>("Failed to retrieve request");
        }
    }

    public async Task<IApiResponse<ServiceRequestDto>> UpdateRequestAsync(string requestId, UpdateServiceRequestRequest request, AuthData admin)
    {
        try
        {
            var serviceRequest = await requestRepo.GetByIdAsync(requestId);
            if (serviceRequest is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ServiceRequestDto>("Request not found");

            if (string.IsNullOrWhiteSpace(request.Stage) && string.IsNullOrWhiteSpace(request.Note) && request.Attachment is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestDto>("Provide a stage, a note, or an attachment");

            if (!string.IsNullOrWhiteSpace(request.Stage))
            {
                var serviceType = await serviceTypeRepo.GetByIdAsync(serviceRequest.ServiceTypeId);
                var validStages = serviceType?.Stages ?? [];
                if (!validStages.Contains(request.Stage))
                    return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestDto>($"\"{request.Stage}\" is not one of this service's configured stages.");
            }

            string? attachmentUrl = null;
            if (request.Attachment is { Length: > 0 })
            {
                var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(request.Attachment.FileName)}";
                attachmentUrl = await storageService.UploadFileAsync(request.Attachment, objectName, AttachmentFolderName, currentTenant.InstitutionSlug ?? "");
            }

            var staffName = $"{admin.FirstName} {admin.LastName}";
            serviceRequest.Updates.Add(new ServiceRequestUpdate
            {
                ChangedAt = DateTime.UtcNow,
                Stage = request.Stage,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                AttachmentUrl = attachmentUrl,
                ChangedByStaffName = staffName,
            });

            if (!string.IsNullOrWhiteSpace(request.Stage))
                serviceRequest.CurrentStage = request.Stage;
            if (attachmentUrl is not null)
            {
                var key = serviceRequest.Attachments.Count > 0 ? $"admin_{serviceRequest.Attachments.Count + 1}" : "admin_1";
                serviceRequest.Attachments[key] = attachmentUrl;
            }
            serviceRequest.UpdatedAt = DateTime.UtcNow;
            serviceRequest.UpdatedBy = admin.Id;

            await requestRepo.UpdateAsync(serviceRequest);

            if (!string.IsNullOrWhiteSpace(request.Stage) || attachmentUrl is not null || !string.IsNullOrWhiteSpace(request.Note))
            {
                notificationActor.Tell(new DispatchServiceRequestUpdatedCommand(
                    currentTenant.InstitutionId!, serviceRequest.MemberId, serviceRequest.Id, serviceRequest.RequestNumber,
                    serviceRequest.ServiceTypeName, serviceRequest.CurrentStage));
            }

            logger.LogInformation("Service request {RequestId} updated by admin {AdminId} — stage={Stage}", requestId, admin.Id, request.Stage);
            return serviceRequest.ToDto().ToOkApiResponse("Request updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating service request {RequestId}", requestId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceRequestDto>("Failed to update request");
        }
    }

}

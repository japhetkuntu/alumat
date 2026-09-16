using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Extensions;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;

/// <summary>
/// Member-side request + checkout for the "Alumni Services" catalog.
/// Deliberately mirrors StoreOrderService's shape end to end: same
/// Zero-Deduction platform-fee mechanics, same "record created synchronously
/// at initiation, confirmed by webhook or poll fallback" lifecycle, same
/// OwnsReferenceAsync chain-of-responsibility dispatch from
/// PaystackCallbackActor. The one branch Store doesn't have: a free service
/// (Price == 0) skips Paystack entirely and starts straight in the request's
/// first configured stage.
/// </summary>
public class ServiceRequestService(
    IAlumniPgRepository<ServiceRequest> requestRepo,
    IAlumniPgRepository<ServiceType> serviceTypeRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ICurrentTenantService currentTenant,
    AlumniDbContext db,
    IPaystackService paystackService,
    PaystackConfig paystackConfig,
    IStorageService storageService,
    ITemporalClientProvider temporalProvider,
    IConfiguration configuration,
    ILogger<ServiceRequestService> logger) : IServiceRequestService
{
    private const string AttachmentFolderName = "alumni";
    private readonly string _paystackCallbackUrl = BuildCallbackUrl(configuration);

    private static string BuildCallbackUrl(IConfiguration configuration)
    {
        var callbackUrl = configuration["PaystackConfig:CallbackUrl"] ?? string.Empty;
        if (!string.IsNullOrEmpty(callbackUrl) && !callbackUrl.EndsWith("/callback", StringComparison.OrdinalIgnoreCase))
            callbackUrl = callbackUrl.TrimEnd('/') + "/callback";
        return callbackUrl;
    }

    private async Task<Institution?> GetCurrentInstitutionAsync() =>
        string.IsNullOrEmpty(currentTenant.InstitutionId) ? null : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);

    /// <summary>Mirrors StoreOrderService/ContributionService.BuildZeroDeductionCharge — the institution nets 100% of totalAmount, our fee is collected from the payer's grossed-up charge.</summary>
    private (long amountSubunit, long? transactionCharge, string? bearer, decimal platformFee, decimal gatewayFee, decimal transactionChargeAmount, decimal grossCharge)
        BuildZeroDeductionCharge(decimal totalAmount, Institution? institution)
    {
        var totalSubunit = (long)Math.Round(totalAmount * 100m, MidpointRounding.AwayFromZero);

        if (institution is null || string.IsNullOrEmpty(institution.PaystackSubaccountCode))
            return (totalSubunit, null, null, 0m, 0m, 0m, totalAmount);

        var charge = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            totalSubunit,
            institution.PlatformFeePercentage,
            paystackConfig.GatewayFeePercentage,
            paystackConfig.GatewayFixedFeeSubunit,
            paystackConfig.GatewayFeeCapSubunit,
            paystackConfig.GatewayFeeSafetyBufferSubunit,
            institution.PlatformFeeFlatThreshold.HasValue ? (long)Math.Round(institution.PlatformFeeFlatThreshold.Value * 100m, MidpointRounding.AwayFromZero) : null,
            institution.PlatformFeeFlatAmount.HasValue ? (long)Math.Round(institution.PlatformFeeFlatAmount.Value * 100m, MidpointRounding.AwayFromZero) : null);

        return (
            charge.ChargeAmountSubunit,
            charge.TransactionChargeSubunit,
            "account",
            charge.PlatformFeeSubunit / 100m,
            charge.GatewayFeeSubunit / 100m,
            charge.TransactionChargeSubunit / 100m,
            charge.ChargeAmountSubunit / 100m);
    }

    public async Task<IApiResponse<PgPagedResult<ServiceTypeDto>>> GetServiceTypesAsync(ServiceTypeFilter filter)
    {
        try
        {
            var search = filter.Search?.ToLower();
            var result = await serviceTypeRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                s => s.Status == "Active" && (string.IsNullOrEmpty(search) || s.Name.ToLower().Contains(search)));

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
            if (serviceType is null || serviceType.Status != "Active")
                return ApiResponseExtensions.ToNotFoundApiResponse<ServiceTypeDto>("Service not found");
            return serviceType.ToDto().ToOkApiResponse("Service retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving service type {ServiceTypeId}", serviceTypeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceTypeDto>("Failed to retrieve service");
        }
    }

    public async Task<IApiResponse<ServiceRequestCheckoutResponse>> CreateRequestAsync(CreateServiceRequestRequest request, Dictionary<string, IFormFile> attachments, AuthData member)
    {
        try
        {
            var serviceType = await serviceTypeRepo.GetByIdAsync(request.ServiceTypeId);
            if (serviceType is null || serviceType.Status != "Active")
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestCheckoutResponse>("This service is no longer available.");

            Dictionary<string, string> answers;
            try
            {
                answers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.AnswersJson) ?? [];
            }
            catch
            {
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestCheckoutResponse>("Could not read the submitted form answers.");
            }

            var uploadedAttachments = new Dictionary<string, string>();
            foreach (var field in serviceType.Fields)
            {
                if (field.Type == "File")
                {
                    if (attachments.TryGetValue(field.Key, out var file) && file.Length > 0)
                    {
                        var objectName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
                        uploadedAttachments[field.Key] = await storageService.UploadFileAsync(file, objectName, AttachmentFolderName, currentTenant.InstitutionSlug ?? "");
                    }
                    else if (field.Required)
                    {
                        return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestCheckoutResponse>($"\"{field.Label}\" is required.");
                    }
                }
                else if (field.Required && string.IsNullOrWhiteSpace(answers.GetValueOrDefault(field.Key)))
                {
                    return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestCheckoutResponse>($"\"{field.Label}\" is required.");
                }
            }

            // Services are never free — ServiceService.CreateServiceTypeAsync/UpdateServiceTypeAsync
            // both reject Price <= 0, so this only guards against stale data.
            if (serviceType.Price <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestCheckoutResponse>("This service isn't properly configured. Please contact the institution.");

            var memberSnapshot = new MemberSnapshot
            {
                Id = member.Id,
                FirstName = member.FirstName,
                LastName = member.LastName,
                Email = member.Email,
                ProfilePictureUrl = member.ProfilePictureUrl,
            };

            var currentInstitution = await GetCurrentInstitutionAsync();
            var charge = BuildZeroDeductionCharge(serviceType.Price, currentInstitution);

            logger.LogInformation(
                "Zero-Deduction charge for service request by member {MemberId}, institution {InstitutionId}: amount={Amount}, platformFee={PlatformFee}, gatewayFee={GatewayFee}, chargeAmount={ChargeAmount}",
                member.Id, currentInstitution?.Id, serviceType.Price, charge.platformFee, charge.gatewayFee, charge.grossCharge);

            // Generate our reference and persist the Pending request BEFORE ever
            // telling Paystack about it, so any callback Paystack could possibly
            // send for this reference already has a matching row.
            var reference = PaystackReferencePrefix.NewReference(PaystackReferencePrefix.ServiceRequest);

            var serviceRequest = new ServiceRequest
            {
                RequestNumber = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                MemberId = member.Id,
                Member = memberSnapshot,
                ServiceTypeId = serviceType.Id,
                ServiceTypeName = serviceType.Name,
                Amount = serviceType.Price,
                FieldAnswers = answers,
                Attachments = uploadedAttachments,
                PaymentStatus = "Pending",
                TransactionRef = reference,
                PlatformFeeAmount = charge.platformFee,
                GatewayFeeAmount = charge.gatewayFee,
                TransactionChargeAmount = charge.transactionChargeAmount,
                GrossChargeAmount = charge.grossCharge,
                CurrentStage = "", // set to the service's first stage once payment is confirmed
                CreatedBy = member.Id,
            };

            await requestRepo.AddAsync(serviceRequest);

            var paymentResponse = await paystackService.InitializePaymentAsync(new InitializePaymentRequest
            {
                Reference = reference,
                Email = member.Email,
                Amount = charge.amountSubunit,
                CallbackUrl = !string.IsNullOrWhiteSpace(request.CallbackUrl) ? request.CallbackUrl : _paystackCallbackUrl,
                Metadata = new Dictionary<string, string> { { "memberId", member.Id }, { "serviceRequest", "true" } },
                Subaccount = currentInstitution?.PaystackSubaccountCode,
                TransactionCharge = charge.transactionCharge,
                Bearer = charge.bearer,
            });

            if (!paymentResponse.Status)
            {
                // Paystack never accepted this reference, so it will never send a
                // callback for it either — mark it Failed rather than leaving a
                // phantom Pending row nothing will ever resolve.
                serviceRequest.PaymentStatus = "Failed";
                serviceRequest.FailureMessage = paymentResponse.Message;
                await requestRepo.UpdateAsync(serviceRequest);
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestCheckoutResponse>(paymentResponse.Message);
            }

            logger.LogInformation("Service request checkout initiated for member {MemberId}, request {RequestId}, reference {Reference}", member.Id, serviceRequest.Id, reference);

            return new ServiceRequestCheckoutResponse { AuthorizationUrl = paymentResponse.Data?.AuthorizationUrl, RequestId = serviceRequest.Id, Reference = reference }
                .ToOkApiResponse("Checkout initiated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating service request for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceRequestCheckoutResponse>("Failed to create request");
        }
    }

    public async Task<bool> OwnsReferenceAsync(string reference)
    {
        return await db.Set<ServiceRequest>().IgnoreQueryFilters().AnyAsync(r => r.TransactionRef == reference);
    }


    public async Task<IApiResponse<ServiceRequestStatusResponse>> GetRequestStatusAsync(string reference, AuthData member)
    {
        try
        {
            var request = await requestRepo.GetOneAsync(r => r.TransactionRef == reference);
            if (request is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ServiceRequestStatusResponse>("Request not found");
            if (request.MemberId != member.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<ServiceRequestStatusResponse>("Reference does not belong to the current member");

            if (request.PaymentStatus == "Pending" && temporalProvider.IsAvailable)
            {
                // Fallback in case the webhook hasn't landed yet.
                var handle = await temporalProvider.Client!.StartOrAttachAsync<IProcessServiceRequestCallbackWorkflow, PaymentCallbackResult>(
                    wf => wf.RunAsync(new ProcessPaymentCallbackRequest("Paystack", reference, null!)),
                    $"payment-callback-servicerequest-{reference}", OperationsTaskQueues.PaymentCallbackProcessing);
                await handle.GetResultAsync();
                request = await requestRepo.GetOneAsync(r => r.TransactionRef == reference) ?? request;
            }

            return new ServiceRequestStatusResponse
            {
                Reference = reference,
                PaymentStatus = request.PaymentStatus,
                Amount = request.Amount,
                Message = request.PaymentStatus switch
                {
                    "Successful" => "Payment confirmed",
                    "Pending" => "Payment has been initiated but not yet completed.",
                    _ => request.FailureMessage ?? "Payment failed",
                },
            }.ToOkApiResponse("Request status retrieved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving service request status for reference {Reference}", reference);
            return ApiResponseExtensions.ToServerErrorApiResponse<ServiceRequestStatusResponse>("Failed to retrieve request status");
        }
    }

    public async Task<IApiResponse<PgPagedResult<ServiceRequestDto>>> GetMyRequestsAsync(ServiceRequestFilter filter, string memberId)
    {
        try
        {
            var result = await requestRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => r.MemberId == memberId && (string.IsNullOrEmpty(filter.Stage) || r.CurrentStage == filter.Stage));

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
            logger.LogError(e, "Error retrieving service requests for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ServiceRequestDto>>("Failed to retrieve requests");
        }
    }
}

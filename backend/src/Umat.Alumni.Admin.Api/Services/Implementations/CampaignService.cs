using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Storage.Sdk.Services;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class CampaignService(
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Member> memberRepo,
    IStorageService storageService,
    ILogger<CampaignService> logger) : ICampaignService
{
    public async Task<IApiResponse<PgPagedResult<CampaignDto>>> GetCampaignsAsync(CampaignFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetCampaigns request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;

            CampaignStatus? status = null;
            if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<CampaignStatus>(filter.Status, true, out var parsedStatus))
            {
                status = parsedStatus;
            }

            var result = await campaignRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                c => (!status.HasValue || c.Status == status.Value)
                  && (isSuper || (yearGroup.HasValue && c.YearGroups != null && c.YearGroups.Contains(yearGroup.Value))));

            var dtos = result.Results.Select(c => c.ToDto()).ToList();

            // Populate TotalEligibleMembers for membership campaigns (only members whose graduation year <= campaign's membership year)
            foreach (var dto in dtos.Where(d => d.IsMembershipCampaign && d.MembershipYear.HasValue))
            {
                var year = dto.MembershipYear!.Value;
                dto.TotalEligibleMembers = await memberRepo.CountAsync(m => m.Status == "Active" && m.GraduationYear <= year);
            }

            var dtoResult = new PgPagedResult<CampaignDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = dtos,
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving campaigns");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<CampaignDto>>("Failed to retrieve campaigns");
        }
    }

    public async Task<IApiResponse<CampaignDto>> GetCampaignByIdAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetCampaignById request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);
            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;
            if (!isSuper)
            {
                if (!yearGroup.HasValue || campaign.YearGroups == null || !campaign.YearGroups.Contains(yearGroup.Value))
                {
                    logger.LogWarning("Denied campaign view access for admin {AdminId} to campaign {CampaignId} (adminYear={AdminYear}, campaignYears={CampaignYears})",
                        admin.Id, campaignId, admin.GraduationYear, campaign.YearGroups ?? new List<int>());
                    return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");
                }
            }

            var dto = campaign.ToDto();

            if (dto.IsMembershipCampaign && dto.MembershipYear.HasValue)
            {
                var year = dto.MembershipYear.Value;
                dto.TotalEligibleMembers = await memberRepo.CountAsync(m => m.Status == "Active" && m.GraduationYear <= year);
            }

            return dto.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to retrieve campaign");
        }
    }

    public async Task<IApiResponse<CampaignDto>> CreateCampaignAsync(CreateCampaignRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateCampaign request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);

            if (request.IsMembershipCampaign && admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<CampaignDto>("Only super admins can create membership campaigns.");
            if (request.IsMembershipCampaign && !request.MembershipYear.HasValue)
                return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Membership campaigns must set a valid membership year.");

            if (request.TargetAmount <= 0 || request.AmountPerMember <= 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Target amount and amount per member must be greater than zero.");
            if (!request.IsMembershipCampaign && request.Deadline <= DateTime.UtcNow)
                return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Deadline must be a future date/time.");
            var campaign = new Campaign
            {
                Title = request.Title,
                Description = request.Description,
                TargetAmount = request.TargetAmount,
                AmountPerMember = request.AmountPerMember,
                PensionerAmountPerMember = request.IsMembershipCampaign ? request.PensionerAmountPerMember : null,
                Deadline = DateTime.SpecifyKind(request.Deadline, DateTimeKind.Utc),
                Status = CampaignStatus.Active,
                YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups),
                YoutubeVideoUrl = request.YoutubeVideoUrl,
                AllowOnlinePayments = request.AllowOnlinePayments,
                AllowManualPayments = request.AllowManualPayments,
                IsMembershipCampaign = request.IsMembershipCampaign,
                MembershipYear = request.MembershipYear,
                CreatedBy = admin.Id,
            };

            if (!string.IsNullOrWhiteSpace(request.BankAccountNumber) && !string.IsNullOrWhiteSpace(request.BankAccountName) && !string.IsNullOrWhiteSpace(request.BankName))
            {
                campaign.BankAccount = new ManualPaymentBankAccount
                {
                    AccountNumber = request.BankAccountNumber!.Trim(),
                    AccountName = request.BankAccountName!.Trim(),
                    BankName = request.BankName!.Trim(),
                    Branch = request.BankBranch?.Trim() ?? string.Empty,
                };
            }

            if (!string.IsNullOrWhiteSpace(request.MobileMoneyNumber) && !string.IsNullOrWhiteSpace(request.MobileMoneyName) && !string.IsNullOrWhiteSpace(request.MobileMoneyProvider))
            {
                if (Enum.TryParse<MobileMoneyProvider>(request.MobileMoneyProvider!, true, out var provider))
                {
                    campaign.MobileMoneyAccount = new ManualPaymentMobileMoneyAccount
                    {
                        MobileMoneyNumber = request.MobileMoneyNumber!.Trim(),
                        Name = request.MobileMoneyName!.Trim(),
                        Provider = provider,
                    };
                }
            }

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                campaign.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            await campaignRepo.AddAsync(campaign);

            logger.LogInformation("Campaign {CampaignId} created by admin {AdminId}", campaign.Id, admin.Id);
            return campaign.ToDto().ToCreatedApiResponse("Campaign created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating campaign: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to create campaign");
        }
    }

    public async Task<IApiResponse<CampaignDto>> UpdateCampaignAsync(UpdateCampaignRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateCampaign request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);

            var campaign = await campaignRepo.GetByIdAsync(request.CampaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            var canModify = admin.CanModifyYearGroupScopedItem(campaign.YearGroups, campaign.CreatedBy);
            if (!canModify && !(admin.Role == "SuperAdmin" && request.Status == CampaignStatus.Closed.ToString()))
            {
                logger.LogWarning("Denied campaign update access for admin {AdminId} to campaign {CampaignId} (adminYear={AdminYear}, campaignYears={CampaignYears}, createdBy={CreatedBy})",
                    admin.Id, request.CampaignId, admin.GraduationYear, campaign.YearGroups ?? new List<int>(), campaign.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");
            }

            if (request.IsMembershipCampaign && admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToForbiddenApiResponse<CampaignDto>("Only super admins can mark campaigns as membership campaigns.");

            if (request.IsMembershipCampaign && !request.MembershipYear.HasValue)
                return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Membership campaigns must set a valid membership year.");

            campaign.Title = request.Title;
            campaign.Description = request.Description;
            campaign.Deadline = DateTime.SpecifyKind(request.Deadline, DateTimeKind.Utc);
            campaign.IsMembershipCampaign = request.IsMembershipCampaign;
            campaign.MembershipYear = request.MembershipYear;
            campaign.PensionerAmountPerMember = request.IsMembershipCampaign ? request.PensionerAmountPerMember : null;
            if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<CampaignStatus>(request.Status, true, out var updatedStatus))
            {
                // Prevent reopening a closed campaign
                if (campaign.Status == CampaignStatus.Closed && updatedStatus == CampaignStatus.Active)
                    return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Closed campaigns cannot be reopened.");

                campaign.Status = updatedStatus;
            }
            else if (!string.IsNullOrWhiteSpace(request.Status))
            {
                return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Invalid campaign status provided.");
            }

            if (request.TargetAmount.HasValue)
            {
                if (request.TargetAmount.Value <= 0)
                    return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Target amount must be greater than zero.");
                campaign.TargetAmount = request.TargetAmount.Value;
            }
            if (request.AmountPerMember.HasValue)
            {
                if (request.AmountPerMember.Value <= 0)
                    return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Amount per member must be greater than zero.");
                campaign.AmountPerMember = request.AmountPerMember.Value;
            }

            campaign.YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            campaign.YoutubeVideoUrl = request.YoutubeVideoUrl;
            campaign.AllowOnlinePayments = request.AllowOnlinePayments;
            campaign.AllowManualPayments = request.AllowManualPayments;

            if (!string.IsNullOrWhiteSpace(request.BankAccountNumber) && !string.IsNullOrWhiteSpace(request.BankAccountName) && !string.IsNullOrWhiteSpace(request.BankName))
            {
                campaign.BankAccount = new ManualPaymentBankAccount
                {
                    AccountNumber = request.BankAccountNumber!.Trim(),
                    AccountName = request.BankAccountName!.Trim(),
                    BankName = request.BankName!.Trim(),
                    Branch = request.BankBranch?.Trim() ?? string.Empty,
                };
            }
            else
            {
                campaign.BankAccount = null;
            }

            if (!string.IsNullOrWhiteSpace(request.MobileMoneyNumber) && !string.IsNullOrWhiteSpace(request.MobileMoneyName) && !string.IsNullOrWhiteSpace(request.MobileMoneyProvider))
            {
                if (Enum.TryParse<MobileMoneyProvider>(request.MobileMoneyProvider!, true, out var provider))
                {
                    campaign.MobileMoneyAccount = new ManualPaymentMobileMoneyAccount
                    {
                        MobileMoneyNumber = request.MobileMoneyNumber!.Trim(),
                        Name = request.MobileMoneyName!.Trim(),
                        Provider = provider,
                    };
                }
            }
            else
            {
                campaign.MobileMoneyAccount = null;
            }

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                campaign.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            campaign.UpdatedAt = DateTime.UtcNow;
            campaign.UpdatedBy = admin.Id;
            await campaignRepo.UpdateAsync(campaign);

            logger.LogInformation("Campaign {CampaignId} updated by admin {AdminId}", campaign.Id, admin.Id);
            return campaign.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating campaign {CampaignId} by admin {AdminId}", request.CampaignId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to update campaign");
        }
    }

    public async Task<IApiResponse<object>> DeleteCampaignAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("DeleteCampaign request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Campaign not found");

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;
            var canModify = isSuper
                ? (campaign.YearGroups == null || campaign.YearGroups.Count == 0 || campaign.CreatedBy == admin.Id)
                : (yearGroup.HasValue && campaign.YearGroups != null && campaign.YearGroups.Contains(yearGroup.Value));

            if (!canModify)
            {
                logger.LogWarning("Denied campaign delete access for admin {AdminId} to campaign {CampaignId} (adminYear={AdminYear}, campaignYears={CampaignYears}, createdBy={CreatedBy})",
                    admin.Id, campaignId, admin.GraduationYear, campaign.YearGroups ?? new List<int>(), campaign.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Campaign not found");
            }

            await campaignRepo.RemoveAsync(campaign);

            logger.LogInformation("Campaign {CampaignId} deleted", campaignId);
            return new object().ToOkApiResponse("Campaign deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete campaign");
        }
    }

    public async Task<IApiResponse<CampaignDto>> ArchiveCampaignAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("ArchiveCampaign request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;
            var canModify = isSuper
                ? (campaign.YearGroups == null || campaign.YearGroups.Count == 0 || campaign.CreatedBy == admin.Id)
                : (yearGroup.HasValue && campaign.YearGroups != null && campaign.YearGroups.Contains(yearGroup.Value));

            if (!canModify)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            campaign.Status = CampaignStatus.Archived;
            campaign.UpdatedAt = DateTime.UtcNow;
            await campaignRepo.UpdateAsync(campaign);

            logger.LogInformation("Campaign {CampaignId} archived", campaignId);
            return campaign.ToDto().ToOkApiResponse("Campaign archived");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error archiving campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to archive campaign");
        }
    }

    public async Task<IApiResponse<CampaignDto>> UnarchiveCampaignAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("UnarchiveCampaign request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;
            var canModify = isSuper
                ? (campaign.YearGroups == null || campaign.YearGroups.Count == 0 || campaign.CreatedBy == admin.Id)
                : (yearGroup.HasValue && campaign.YearGroups != null && campaign.YearGroups.Contains(yearGroup.Value));

            if (!canModify)
            {
                logger.LogWarning("Denied campaign unarchive access for admin {AdminId} to campaign {CampaignId} (adminYear={AdminYear}, campaignYears={CampaignYears}, createdBy={CreatedBy})",
                    admin.Id, campaignId, admin.GraduationYear, campaign.YearGroups ?? new List<int>(), campaign.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");
            }

            campaign.Status = CampaignStatus.Closed;
            campaign.UpdatedAt = DateTime.UtcNow;
            await campaignRepo.UpdateAsync(campaign);

            logger.LogInformation("Campaign {CampaignId} unarchived", campaignId);
            return campaign.ToDto().ToOkApiResponse("Campaign unarchived");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unarchiving campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to unarchive campaign");
        }
    }

    public async Task<IApiResponse<CampaignDto>> ActivateCampaignAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("ActivateCampaign request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;
            var canModify = isSuper
                ? (campaign.YearGroups == null || campaign.YearGroups.Count == 0 || campaign.CreatedBy == admin.Id)
                : (yearGroup.HasValue && campaign.YearGroups != null && campaign.YearGroups.Contains(yearGroup.Value));

            if (!canModify)
            {
                logger.LogWarning("Denied campaign activate access for admin {AdminId} to campaign {CampaignId} (adminYear={AdminYear}, campaignYears={CampaignYears}, createdBy={CreatedBy})",
                    admin.Id, campaignId, admin.GraduationYear, campaign.YearGroups ?? new List<int>(), campaign.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<CampaignDto>("Campaign not found");
            }

            if (campaign.Status == CampaignStatus.Closed)
                return ApiResponseExtensions.ToBadRequestApiResponse<CampaignDto>("Closed campaigns cannot be reopened.");

            campaign.Status = CampaignStatus.Active;
            campaign.UpdatedAt = DateTime.UtcNow;
            await campaignRepo.UpdateAsync(campaign);

            logger.LogInformation("Campaign {CampaignId} activated", campaignId);
            return campaign.ToDto().ToOkApiResponse("Campaign activated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error activating campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<CampaignDto>("Failed to activate campaign");
        }
    }

    public async Task<IApiResponse<PaystackDisbursementSummaryDto>> GetCampaignPaystackSummaryAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetCampaignPaystackSummary request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PaystackDisbursementSummaryDto>("Campaign not found");

            if (admin.Role != "SuperAdmin" && (admin.GraduationYear == null || campaign.YearGroups == null || !campaign.YearGroups.Contains(admin.GraduationYear.Value)) && campaign.CreatedBy != admin.Id)
            {
                logger.LogWarning("Denied campaign paystack summary access for admin {AdminId} to campaign {CampaignId}", admin.Id, campaignId);
                return ApiResponseExtensions.ToNotFoundApiResponse<PaystackDisbursementSummaryDto>("Campaign not found");
            }

            var paystackContributions = await contributionRepo.GetAllAsync(c => c.CampaignId == campaignId && c.PaymentMethod == "Paystack" && c.Status == "Confirmed");

            var totalPaidToPaystack = paystackContributions.Sum(c => c.Amount);
            var totalDisbursed = campaign.IsPaystackDisbursed ? totalPaidToPaystack : 0m;
            var totalOutstanding = campaign.IsPaystackDisbursed ? 0m : totalPaidToPaystack;

            var summary = new PaystackDisbursementSummaryDto
            {
                TotalPaidToPaystack = totalPaidToPaystack,
                TotalDisbursed = totalDisbursed,
                TotalOutstanding = totalOutstanding,
                ConfirmedCount = paystackContributions.Count(),
                DisbursedCount = campaign.IsPaystackDisbursed ? paystackContributions.Count() : 0,
            };
            return summary.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting paystack summary for campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PaystackDisbursementSummaryDto>("Failed to retrieve paystack summary");
        }
    }

    public async Task<IApiResponse<object>> MarkCampaignPaystackDisbursedAsync(string campaignId, AuthData admin)
    {
        try
        {
            logger.LogInformation("MarkCampaignPaystackDisbursed request for campaignId: {CampaignId} (admin: {AdminId})", campaignId, admin.Id);

            if (admin.Role != "SuperAdmin")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Only super admins can perform this action");

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Campaign not found");

            if (campaign.Status != CampaignStatus.Closed)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Can only mark Paystack disbursement on closed campaigns.");

            if (campaign.IsPaystackDisbursed)
                return new object().ToOkApiResponse("Campaign already marked as paystack disbursed");

            campaign.IsPaystackDisbursed = true;
            campaign.PaystackDisbursedAt = DateTime.UtcNow;
            campaign.PaystackDisbursedBy = admin.Id;
            campaign.UpdatedAt = DateTime.UtcNow;
            campaign.UpdatedBy = admin.Id;

            await campaignRepo.UpdateAsync(campaign);

            logger.LogInformation("Marked campaign {CampaignId} as Paystack disbursed by admin {AdminId}", campaignId, admin.Id);
            return new object().ToOkApiResponse("Campaign paystack contributions marked as disbursed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error marking campaign paystack contributions as disbursed for campaign {CampaignId}", campaignId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to mark campaign paystack contributions as disbursed");
        }
    }
}


public class ContributionService(
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Member> memberRepo,
    ILogger<ContributionService> logger) : IContributionService
{
    public async Task<IApiResponse<PgPagedResult<ContributionDto>>> GetContributionsAsync(ContributionAdminFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetContributions request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            var isSuper = admin.Role == "SuperAdmin";

            var adminCampaignIds = isSuper
                ? new List<string>()
                : (await campaignRepo.GetAllAsync(c => c.CreatedBy == admin.Id)).Select(c => c.Id).ToList();

            var result = await contributionRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                c => (isSuper || c.CreatedBy == admin.Id || adminCampaignIds.Contains(c.CampaignId))
                  && (string.IsNullOrEmpty(filter.CampaignId) || c.CampaignId == filter.CampaignId)
                  && (string.IsNullOrEmpty(filter.Status) || c.Status == filter.Status)
                  && (search == null
                      || (c.TransactionRef != null && c.TransactionRef.Contains(search))
                      || (c.MemberId != null && c.MemberId.Contains(search))
                      || (c.Notes != null && c.Notes.Contains(search))));

            // Backfill missing snapshots for contributions stored before jsonb columns were added.
            var needsBackfill = result.Results.Where(c => c.Member is null || c.Campaign is null).ToList();
            if (needsBackfill.Count > 0)
            {
                var memberIds = needsBackfill.Where(c => c.Member is null).Select(c => c.MemberId).Distinct().ToList();
                var campaignIds = needsBackfill.Where(c => c.Campaign is null).Select(c => c.CampaignId).Distinct().ToList();

                var members = memberIds.Count > 0
                    ? (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id)
                    : new Dictionary<string, Member>();
                var campaigns = campaignIds.Count > 0
                    ? (await campaignRepo.GetAllAsync(c => campaignIds.Contains(c.Id))).ToDictionary(c => c.Id)
                    : new Dictionary<string, Campaign>();

                foreach (var c in needsBackfill)
                {
                    if (c.Member is null && members.TryGetValue(c.MemberId, out var m))
                        c.Member = new MemberSnapshot { Id = m.Id, FirstName = m.FirstName, LastName = m.LastName, Email = m.Email, ProfilePictureUrl = m.ProfilePictureUrl };
                    if (c.Campaign is null && campaigns.TryGetValue(c.CampaignId, out var cam))
                        c.Campaign = new CampaignSnapshot { Id = cam.Id, Title = cam.Title };
                }

                await contributionRepo.UpdateRangeAsync(needsBackfill);
            }

            var dtoResult = new PgPagedResult<ContributionDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(c => c.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving contributions — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ContributionDto>>("Failed to retrieve contributions");
        }
    }

    public async Task<IApiResponse<ContributionDto>> RecordManualContributionAsync(
        RecordManualContributionRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("RecordManualContribution request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);

            Member? member = null;

            if (string.IsNullOrWhiteSpace(request.MemberNumber))
            {
                return ApiResponseExtensions.ToBadRequestApiResponse<ContributionDto>("MemberNumber is required for manual contributions.");
            }

            member = await memberRepo.GetOneAsync(m => m.MemberNumber == request.MemberNumber);

            string memberId;
            MemberSnapshot? memberSnapshot = null;

            if (member is not null)
            {
                memberId = member.Id;
                memberSnapshot = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                    MemberNumber = member.MemberNumber,
                };
            }
            else
            {
                memberId = request.MemberNumber!.Trim();

                var names = (request.MemberName ?? string.Empty).Trim();
                var firstName = string.Empty;
                var lastName = string.Empty;

                if (!string.IsNullOrEmpty(names))
                {
                    var parts = names.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    firstName = parts.FirstOrDefault() ?? string.Empty;
                    lastName = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
                }

                if (string.IsNullOrEmpty(firstName))
                {
                    firstName = request.MemberNumber!;
                }

                memberSnapshot = new MemberSnapshot
                {
                    Id = memberId,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = request.MemberEmail,
                    ProfilePictureUrl = null,
                    MemberNumber = request.MemberNumber,
                };
            }

            var campaign = await campaignRepo.GetByIdAsync(request.CampaignId);
            if (campaign is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ContributionDto>("Campaign not found");

            var contribution = new Contribution
            {
                CampaignId = request.CampaignId,
                Campaign = new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                MemberId = memberId,
                Member = memberSnapshot,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                TransactionRef = request.TransactionRef,
                Notes = request.Notes,
                Status = request.Confirmed ? "Confirmed" : "Pending",
                ConfirmedAt = request.Confirmed ? (request.PaidAt ?? DateTime.UtcNow) : null,
                ConfirmedBy = request.Confirmed ? admin.Id : null,
                CreatedBy = admin.Id,
            };
            await contributionRepo.AddAsync(contribution);

            if (request.Confirmed)
            {
                campaign.CollectedAmount += request.Amount;
                campaign.PaidCount += 1;
                await campaignRepo.UpdateAsync(campaign);
            }

            logger.LogInformation("Contribution {ContributionId} recorded by admin {AdminId} (confirmed={Confirmed})", contribution.Id, admin.Id, request.Confirmed);
            return contribution.ToDto().ToCreatedApiResponse("Contribution recorded");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error recording manual contribution: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ContributionDto>("Failed to record contribution");
        }
    }

    public async Task<IApiResponse<object>> ConfirmContributionAsync(string contributionId, AuthData admin)
    {
        try
        {
            logger.LogInformation("ConfirmContribution request for contributionId: {ContributionId} by admin {AdminId}", contributionId, admin.Id);

            var contribution = await contributionRepo.GetByIdAsync(contributionId);
            if (contribution is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Contribution not found");

            var isSuper = admin.Role == "SuperAdmin";
            if (!isSuper && contribution.CreatedBy != admin.Id)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Contribution not found");

            if (contribution.Status == "Confirmed")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Already confirmed");

            contribution.Status = "Confirmed";
            contribution.ConfirmedAt = DateTime.UtcNow;
            contribution.ConfirmedBy = admin.Id;
            contribution.UpdatedAt = DateTime.UtcNow;
            await contributionRepo.UpdateAsync(contribution);

            // Update campaign
            var campaign = await campaignRepo.GetByIdAsync(contribution.CampaignId);
            if (campaign is not null)
            {
                campaign.CollectedAmount += contribution.Amount;
                campaign.PaidCount += 1;
                await campaignRepo.UpdateAsync(campaign);
            }

            logger.LogInformation("Contribution {ContributionId} confirmed by admin {AdminId}", contributionId, admin.Id);
            return new object().ToOkApiResponse("Contribution confirmed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error confirming contribution {ContributionId} by admin {AdminId}", contributionId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to confirm contribution");
        }
    }

    public async Task<IApiResponse<object>> RejectContributionAsync(string contributionId, string? reason, AuthData admin)
    {
        try
        {
            logger.LogInformation("RejectContribution request for contributionId: {ContributionId} by admin {AdminId}", contributionId, admin.Id);

            var contribution = await contributionRepo.GetByIdAsync(contributionId);
            if (contribution is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Contribution not found");

            var isSuper = admin.Role == "SuperAdmin";
            if (!isSuper && contribution.CreatedBy != admin.Id)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Contribution not found");

            contribution.Status = "Rejected";
            contribution.Notes = string.IsNullOrEmpty(reason) ? contribution.Notes : reason;
            contribution.UpdatedAt = DateTime.UtcNow;
            contribution.UpdatedBy = admin.Id;
            await contributionRepo.UpdateAsync(contribution);

            logger.LogInformation("Contribution {ContributionId} rejected by admin {AdminId}", contributionId, admin.Id);
            return new object().ToOkApiResponse("Contribution rejected");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting contribution {ContributionId} by admin {AdminId}", contributionId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reject contribution");
        }
    }
}

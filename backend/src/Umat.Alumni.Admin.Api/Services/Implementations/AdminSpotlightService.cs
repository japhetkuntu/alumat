using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class AdminSpotlightService(
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    INotificationDispatcher notifDispatcher,
    ILogger<AdminSpotlightService> logger) : IAdminSpotlightService
{
    public async Task<IApiResponse<PgPagedResult<SpotlightDto>>> GetSpotlightsAsync(int page, int pageSize, string? status)
    {
        try
        {
            var result = await spotlightRepo.GetPagedAsync(
                page, pageSize, "CreatedAt", "desc",
                status is not null ? s => s.Status == status : null);

            var dtos = result.Results.Select(s => s.ToDto()).ToList();

            var memberIds = dtos.Select(d => d.MemberId).Distinct().ToList();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtos)
            {
                if (members.TryGetValue(dto.MemberId, out var m))
                    dto.MemberGraduationYear = m.GraduationYear;
            }

            return new PgPagedResult<SpotlightDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = dtos,
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving spotlights");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<SpotlightDto>>("Failed to retrieve spotlights");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> CreateSpotlightAsync(AdminCreateSpotlightRequest request, AuthData admin)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(request.MemberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Member not found");

            var spotlight = new Spotlight
            {
                MemberId = member.Id,
                Member = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                    MemberNumber = member.MemberNumber,
                },
                Title = request.Title,
                Story = request.Story,
                ImageUrl = request.ImageUrl,
                Status = "Approved",
                FeaturedMonth = DateTime.UtcNow,
                CreatedBy = admin.Id,
                UpdatedBy = admin.Id,
            };

            await spotlightRepo.AddAsync(spotlight);

            _ = Task.Run(() => notifDispatcher.DispatchSpotlightAlertAsync(spotlight));

            var dto = spotlight.ToDto();
            dto.MemberGraduationYear = member.GraduationYear;
            return dto.ToOkApiResponse("Spotlight created and featured.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating spotlight for member {MemberId}", request.MemberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to create spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> ApproveSpotlightAsync(string spotlightId, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.Status = "Approved";
            spotlight.FeaturedMonth = DateTime.UtcNow;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);

            return spotlight.ToDto().ToOkApiResponse("Spotlight approved.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error approving spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to approve spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> RejectSpotlightAsync(string spotlightId, string? reason, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.Status = "Rejected";
            spotlight.AdminNotes = reason;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);

            return spotlight.ToDto().ToOkApiResponse("Spotlight rejected.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to reject spotlight");
        }
    }
}

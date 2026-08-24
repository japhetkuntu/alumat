using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class DirectoryService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Department> departmentRepo,
    ILogger<DirectoryService> logger) : IDirectoryService
{
    public async Task<IApiResponse<PgPagedResult<DirectoryMemberDto>>> SearchMembersAsync(DirectoryFilter filter)
    {
        try
        {
            logger.LogInformation("SearchMembers request — filter: {Filter}", filter.Serialize());
            var result = await memberRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "FirstName", filter.SortDir ?? "asc",
                m => m.Status == "Active"
                  && (string.IsNullOrEmpty(filter.Search)
                      || m.FirstName.Contains(filter.Search) || m.LastName.Contains(filter.Search)
                      || (m.Company != null && m.Company.Contains(filter.Search)))
                  && (string.IsNullOrEmpty(filter.DepartmentId) || m.DepartmentId == filter.DepartmentId)
                  && (!filter.GraduationYear.HasValue || m.GraduationYear == filter.GraduationYear.Value));

            var departmentIds = result.Results.Select(m => m.DepartmentId).Distinct().ToList();
            var departments = await departmentRepo.GetAllAsync(d => departmentIds.Contains(d.Id));
            var deptLookup = departments.ToDictionary(d => d.Id, d => d.Name);

            var dtos = result.Results.Select(m =>
            {
                var dto = m.ToDto();
                dto.DepartmentName = deptLookup.GetValueOrDefault(m.DepartmentId);
                return dto;
            }).ToList();

            var dtoResult = new PgPagedResult<DirectoryMemberDto>
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
            logger.LogError(e, "Error searching members");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<DirectoryMemberDto>>("Failed to search members");
        }
    }
}

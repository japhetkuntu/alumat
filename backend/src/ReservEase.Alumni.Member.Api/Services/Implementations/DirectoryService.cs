using Microsoft.EntityFrameworkCore;
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
            var search = filter.Search?.ToLower();
            var result = await memberRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "FirstName", filter.SortDir ?? "asc",
                m => m.Status == "Active"
                  && (string.IsNullOrEmpty(search)
                      || m.FirstName.ToLower().Contains(search) || m.LastName.ToLower().Contains(search)
                      || (m.Company != null && m.Company.ToLower().Contains(search)))
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

    // A pin-per-member map stops being usable (or even renderable without
    // clustering) well before this many points, and an unbounded query here
    // was one of very few endpoints in the codebase with no cap at all — at
    // 100k members with map opt-in this could serialize tens of thousands of
    // rows in a single response. Capping is the right fix, not classic
    // page/pageSize — nobody "pages" through a map.
    private const int MaxMapMembers = 5000;

    public async Task<IApiResponse<List<AlumniMapMemberDto>>> GetMapMembersAsync()
    {
        try
        {
            var members = await memberRepo.GetQueryable(m =>
                    m.Status == "Active" && m.ShowOnAlumniMap && m.MapLatitude != null && m.MapLongitude != null)
                .OrderByDescending(m => m.CreatedAt)
                .Take(MaxMapMembers)
                .ToListAsync();

            var dtos = members.Select(m => new AlumniMapMemberDto
            {
                Id = m.Id,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Location = m.Location,
                Latitude = m.MapLatitude!.Value,
                Longitude = m.MapLongitude!.Value,
                GraduationYear = m.GraduationYear,
                ProfilePictureUrl = m.ProfilePictureUrl,
            }).ToList();

            return dtos.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving alumni map members");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<AlumniMapMemberDto>>("Failed to retrieve alumni map");
        }
    }
}

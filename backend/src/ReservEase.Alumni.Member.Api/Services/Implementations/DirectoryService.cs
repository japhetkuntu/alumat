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
    ILogger<DirectoryService> logger,
    IAlumniPgRepository<CommunityMembership>? membershipRepo = null,
    IAlumniPgRepository<Community>? communityRepo = null) : IDirectoryService
{
    private const int SuggestionCount = 6;
    private const int CandidatesPerSignal = 60;

    /// <summary>
    /// A handful of members the caller probably knows, best first. Each shares something real with them: a group, the program they
    /// studied, or the year they joined or graduated. Someone who shares several things ranks above someone who shares one.
    /// </summary>
    public async Task<IApiResponse<List<DirectorySuggestionDto>>> GetSuggestionsAsync(string memberId)
    {
        try
        {
            var me = await memberRepo.GetByIdAsync(memberId);
            if (me is null) return new List<DirectorySuggestionDto>().ToOkApiResponse();

            var reasons = new Dictionary<string, List<string>>();
            var score = new Dictionary<string, int>();
            void Note(string id, string reason, int points)
            {
                if (id == memberId) return;
                if (!reasons.TryGetValue(id, out var list)) reasons[id] = list = [];
                if (!list.Contains(reason)) list.Add(reason);
                score[id] = score.GetValueOrDefault(id) + points;
            }

            // Groups: the strongest signal, since someone chose to join the same one.
            if (membershipRepo is not null && communityRepo is not null)
            {
                var myGroupIds = (await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved")).Select(m => m.CommunityId).Distinct().ToList();
                if (myGroupIds.Count > 0)
                {
                    var names = (await communityRepo.GetAllAsync(c => myGroupIds.Contains(c.Id) && c.IsActive)).ToDictionary(c => c.Id, c => c.Name);
                    var shared = await membershipRepo.GetQueryable(m => m.Status == "Approved" && myGroupIds.Contains(m.CommunityId) && m.MemberId != memberId)
                        .Select(m => new { m.MemberId, m.CommunityId }).Take(CandidatesPerSignal * 3).ToListAsync();
                    foreach (var s in shared.Where(s => names.ContainsKey(s.CommunityId))) Note(s.MemberId, $"In {names[s.CommunityId]}", 3);
                }
            }

            if (!string.IsNullOrWhiteSpace(me.Program))
            {
                var program = me.Program.Trim();
                var same = await memberRepo.GetQueryable(m => m.Status == "Active" && m.Id != memberId && m.Program == program)
                    .Select(m => m.Id).Take(CandidatesPerSignal).ToListAsync();
                foreach (var id in same) Note(id, $"Studied {program}", 2);
            }

            if (me.GraduationYear > 0)
            {
                var year = me.GraduationYear;
                var classmates = await memberRepo.GetQueryable(m => m.Status == "Active" && m.Id != memberId && m.GraduationYear == year)
                    .OrderByDescending(m => m.ProfilePictureUrl != null).Select(m => m.Id).Take(CandidatesPerSignal).ToListAsync();
                foreach (var id in classmates) Note(id, $"Class of {year}", 2);
            }

            var topIds = score.OrderByDescending(kv => kv.Value).Take(SuggestionCount * 2).Select(kv => kv.Key).ToList();
            if (topIds.Count == 0) return new List<DirectorySuggestionDto>().ToOkApiResponse();

            var people = (await memberRepo.GetAllAsync(m => topIds.Contains(m.Id) && m.Status == "Active")).ToDictionary(m => m.Id);
            var result = topIds.Where(people.ContainsKey)
                // A photo breaks ties: a face is what makes someone recognisable.
                .OrderByDescending(id => score[id] * 10 + (people[id].ProfilePictureUrl is null ? 0 : 1))
                .Take(SuggestionCount)
                .Select(id => new DirectorySuggestionDto { Member = people[id].ToDto(), Reasons = reasons[id] })
                .ToList();
            return result.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building suggestions for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<DirectorySuggestionDto>>("Failed to load suggestions");
        }
    }

    public async Task<IApiResponse<PgPagedResult<DirectoryMemberDto>>> SearchMembersAsync(DirectoryFilter filter)
    {
        try
        {
            logger.LogInformation("SearchMembers request — filter: {Filter}", filter.Serialize());
            var search = filter.Search is null ? null : string.Join(" ", filter.Search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLower();
            var result = await memberRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "FirstName", filter.SortDir ?? "asc",
                m => m.Status == "Active"
                  && TextSearch.Matches(search, m.FirstName, m.LastName, (m.FirstName + " " + m.LastName), (m.LastName + " " + m.FirstName), m.Company)
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

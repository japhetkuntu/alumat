using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IDirectoryService
{
    Task<IApiResponse<PgPagedResult<DirectoryMemberDto>>> SearchMembersAsync(DirectoryFilter filter);
    Task<IApiResponse<List<AlumniMapMemberDto>>> GetMapMembersAsync();
}

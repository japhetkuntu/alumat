using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IDirectoryService
{
    Task<IApiResponse<PgPagedResult<DirectoryMemberDto>>> SearchMembersAsync(DirectoryFilter filter);
}

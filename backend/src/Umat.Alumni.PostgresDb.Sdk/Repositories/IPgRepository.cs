using System.Linq.Expressions;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.PostgresDb.Sdk.Repositories;

public interface IPgRepository<T, TContext>
{
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null);
    Task<T?> GetByIdAsync(string id);
    Task<T?> GetOneAsync(Expression<Func<T, bool>> predicate);
    Task<int> AddAsync(T entity);
    Task<int> AddRangeAsync(List<T> entities);
    Task<int> RemoveAsync(T entity);
    Task<int> UpdateAsync(T entity);
    Task<int> UpdateRangeAsync(List<T> entities);
    IQueryable<T> GetQueryable(Expression<Func<T, bool>>? predicate = null);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task<PgPagedResult<T>> GetPagedAsync(int pageIndex, int pageSize, string sortColumn = "Id", string sortDir = "desc", Expression<Func<T, bool>>? filter = null);
}

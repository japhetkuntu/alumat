using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.PostgresDb.Sdk.Repositories;

public interface IPgRepository<T, TContext>
{
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null, bool ignoreQueryFilters = false);
    Task<T?> GetByIdAsync(string id, bool ignoreQueryFilters = false);
    Task<T?> GetOneAsync(Expression<Func<T, bool>> predicate, bool ignoreQueryFilters = false);
    Task<int> AddAsync(T entity);
    /// <summary>Stops tracking an entity. Used after a failed save, so the rejected row is not retried by the next save on the same context.</summary>
    void Detach(T entity);
    Task<int> AddRangeAsync(List<T> entities);
    Task<int> RemoveAsync(T entity);
    Task<int> UpdateAsync(T entity);
    Task<int> UpdateRangeAsync(List<T> entities);
    IQueryable<T> GetQueryable(Expression<Func<T, bool>>? predicate = null, bool ignoreQueryFilters = false);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool ignoreQueryFilters = false);
    Task<PgPagedResult<T>> GetPagedAsync(int pageIndex, int pageSize, string sortColumn = "Id", string sortDir = "desc", Expression<Func<T, bool>>? filter = null, bool ignoreQueryFilters = false);

    /// <summary>Atomic, single-round-trip partial update (EF Core ExecuteUpdateAsync) for
    /// callers that must change specific columns without pulling the full entity into
    /// change tracking — e.g. concurrent stock decrements. Bypasses the tenant query
    /// filter like the rest of this interface's ignoreQueryFilters overloads, since the
    /// predicate is expected to already identify the row precisely (usually by Id).</summary>
    Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls, bool ignoreQueryFilters = false);

    /// <summary>Deletes every matching row in one statement, without loading any of them. For replacing a whole set of child rows, where
    /// removing them one by one is a round trip each.</summary>
    Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, bool ignoreQueryFilters = false);

    /// <summary>Runs several saves as one all-or-nothing unit: if <paramref name="work"/> throws, everything it saved is rolled back. Every repository
    /// of a request shares one context, so saves made through other repositories inside <paramref name="work"/> join the same transaction.</summary>
    Task RunInTransactionAsync(Func<Task> work);
}

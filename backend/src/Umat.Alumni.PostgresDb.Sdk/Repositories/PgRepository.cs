using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.PostgresDb.Sdk.Repositories;

public partial class PgRepository<T, TContext>(TContext context) : IPgRepository<T, TContext>
    where T : class
    where TContext : DbContext
{
    protected readonly TContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex SafeColumnNameRegex();

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> query = _dbSet;
        if (predicate is not null) query = query.Where(predicate);
        return await query.ToListAsync();
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<T?> GetOneAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public async Task<int> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return await _context.SaveChangesAsync();
    }

    public async Task<int> AddRangeAsync(List<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
        return await _context.SaveChangesAsync();
    }

    public async Task<int> RemoveAsync(T entity)
    {
        _dbSet.Remove(entity);
        return await _context.SaveChangesAsync();
    }

    public async Task<int> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return await _context.SaveChangesAsync();
    }

    public async Task<int> UpdateRangeAsync(List<T> entities)
    {
        _dbSet.UpdateRange(entities);
        return await _context.SaveChangesAsync();
    }

    public IQueryable<T> GetQueryable(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> query = _dbSet;
        if (predicate is not null) query = query.Where(predicate);
        return query;
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> query = _dbSet;
        if (predicate is not null) query = query.Where(predicate);
        return await query.CountAsync();
    }

    public async Task<PgPagedResult<T>> GetPagedAsync(
        int pageIndex, int pageSize,
        string sortColumn = "Id", string sortDir = "desc",
        Expression<Func<T, bool>>? filter = null)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        // Validate sort column to prevent SQL injection via dynamic LINQ
        if (!SafeColumnNameRegex().IsMatch(sortColumn))
            sortColumn = "Id";

        // Validate sort direction
        sortDir = sortDir?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "asc" : "desc";

        IQueryable<T> query = _dbSet;
        if (filter is not null) query = query.Where(filter);

        var totalCount = await query.CountAsync();

        var sortExpression = $"{sortColumn} {sortDir}";
        query = query.OrderBy(sortExpression);

        var items = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var lowerBound = ((pageIndex - 1) * pageSize) + 1;
        var upperBound = Math.Min(pageIndex * pageSize, totalCount);

        return new PgPagedResult<T>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Count = items.Count,
            TotalCount = totalCount,
            TotalPages = totalPages,
            LowerBoundSize = (int)lowerBound,
            UpperBoundSize = (int)upperBound,
            Results = items,
        };
    }
}

using System.Linq.Expressions;
using System.Reflection;

namespace ReservEase.Alumni.PostgresDb.Sdk.Extensions;

/// <summary>
/// Search that behaves the way people type. "esi owusu" finds Esi Owusu, "engineer acme" finds a job titled
/// Engineer at Acme, and extra spaces or capital letters don't matter. Each word must appear in at least one
/// of the fields, in any order.
///
/// Use it inside a repository predicate: <c>x =&gt; TextSearch.Matches(search, x.Title, x.Company)</c>.
/// The repository rewrites the call into plain LIKE comparisons before it reaches the database, so it stays
/// index-friendly. On a query the repository did not build, call <see cref="Rewrite{T}"/> on the predicate or
/// use <see cref="WhereMatches{T}"/>.
/// </summary>
public static class TextSearch
{
    private const int MaxWords = 6;

    /// <summary>Marker for predicates. Also works in memory, so it is safe in tests.</summary>
    public static bool Matches(string? search, params string?[] fields)
    {
        var words = Words(search);
        if (words.Length == 0) return true;
        return words.All(w => fields.Any(f => f != null && f.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The words in a search box entry: lower case, single spaces, capped so a pasted paragraph stays cheap.</summary>
    public static string[] Words(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : search.ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .Take(MaxWords)
                .ToArray();

    public static IQueryable<T> WhereMatches<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate) =>
        query.Where(Rewrite(predicate));

    public static Expression<Func<T, bool>> Rewrite<T>(Expression<Func<T, bool>> predicate) =>
        (Expression<Func<T, bool>>)new Rewriter().Visit(predicate);

    private static readonly MethodInfo MatchesMethod = typeof(TextSearch).GetMethod(nameof(Matches))!;
    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private sealed class Rewriter : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method != MatchesMethod) return base.VisitMethodCall(node);

            // The search text is a captured variable, so it can be read now and turned into constants.
            var search = Expression.Lambda<Func<string?>>(node.Arguments[0]).Compile()();
            var words = Words(search);
            if (words.Length == 0) return Expression.Constant(true);

            var fields = node.Arguments[1] is NewArrayExpression array
                ? array.Expressions.Select(Visit).ToList()
                : [Visit(node.Arguments[1])];

            Expression? all = null;
            foreach (var word in words)
            {
                var wordConstant = Expression.Constant(word);
                Expression? any = null;
                foreach (var field in fields)
                {
                    Expression hit = Expression.Call(Expression.Call(field, ToLowerMethod), ContainsMethod, wordConstant);
                    // A column that can be empty must be checked first, so a missing value never counts as a match.
                    if (IsNullable(field))
                        hit = Expression.AndAlso(Expression.NotEqual(field, Expression.Constant(null, typeof(string))), hit);
                    any = any is null ? hit : Expression.OrElse(any, hit);
                }
                if (any is null) return Expression.Constant(false);
                all = all is null ? any : Expression.AndAlso(all, any);
            }
            return all!;
        }

        // Concatenations such as (First + " " + Last) are never null. Plain properties might be.
        private static bool IsNullable(Expression e) => e is MemberExpression or MethodCallExpression;
    }
}

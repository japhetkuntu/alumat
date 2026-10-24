using Microsoft.EntityFrameworkCore;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>Finding the next member number (SLUG-YEAR-NNNN) without reading every member who already has one.</summary>
public static class MemberNumbers
{
    /// <summary>How many of the highest numbers are read. Only an unreadable suffix could push the real maximum out of this window.</summary>
    private const int Window = 25;

    /// <summary>
    /// The highest sequence already used under <paramref name="prefix"/>, or 0 when there is none. The database picks the longest, then
    /// highest, numbers, so only a handful of rows come back instead of every member in the year group. A number whose suffix is not a
    /// whole number counts as 0, as it always has.
    /// </summary>
    public static async Task<int> MaxSequenceAsync(IQueryable<MemberEntity> members, string prefix)
    {
        var withPrefix = members.Where(m => m.MemberNumber != null && m.MemberNumber.StartsWith(prefix));

        var top = await withPrefix
            .OrderByDescending(m => m.MemberNumber!.Length).ThenByDescending(m => m.MemberNumber)
            .Select(m => m.MemberNumber!).Take(Window).ToListAsync();

        var max = Parse(top, prefix);
        if (max > 0 || top.Count < Window) return max;

        // Every number in the window was unreadable (never seen in practice): fall back to reading them all.
        return Parse(await withPrefix.Select(m => m.MemberNumber!).ToListAsync(), prefix);
    }

    /// <summary>The number to give the next member, formatted the same way as the existing ones.</summary>
    public static async Task<string> NextAsync(IQueryable<MemberEntity> members, string prefix) =>
        $"{prefix}{(await MaxSequenceAsync(members, prefix) + 1):D4}";

    private static int Parse(IEnumerable<string> numbers, string prefix) =>
        numbers.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
}

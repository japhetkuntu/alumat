using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ReservEase.Alumni.PostgresDb.Sdk.Repositories;

/// <summary>Telling a database's own refusals apart from real failures, so code can answer "that was already taken" or "that no longer exists" instead of failing.</summary>
public static class DbErrors
{
    public static bool IsUniqueViolation(Exception e) => Has(e, "23505", "UNIQUE constraint failed");
    public static bool IsForeignKeyViolation(Exception e) => Has(e, "23503", "FOREIGN KEY constraint failed");
    /// <summary>The database stopped one of two statements that were each waiting on the other (or could not serialise them). Safe to try again.</summary>
    public static bool IsDeadlock(Exception e) => Has(e, "40P01", "database is deadlocked") || Has(e, "40001", "database is locked");

    private static bool Has(Exception e, string postgresState, string sqliteText)
    {
        for (var x = e; x is not null; x = x.InnerException!)
        {
            if (x is PostgresException pg && pg.SqlState == postgresState) return true;
            if (x is not DbUpdateException && x.Message.Contains(sqliteText, StringComparison.OrdinalIgnoreCase)) return true;
            if (x.InnerException is null) break;
        }
        return false;
    }
}

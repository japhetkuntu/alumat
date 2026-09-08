using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <summary>
    /// Every case-insensitive text search in the codebase filters on
    /// <c>lower(column) LIKE '%term%'</c> (see the many
    /// <c>x.Field.ToLower().Contains(search.ToLower())</c> call sites across
    /// Member.Api/Institution.Api/Platform.Api's search endpoints). A plain
    /// btree index can never serve a leading-wildcard LIKE, and none of these
    /// columns had any index at all for this access pattern — every one of
    /// those searches was a full sequential scan, fine at today's row counts
    /// but a real bottleneck well before 100k members. A GIN trigram index on
    /// the same <c>lower(column)</c> expression the query actually filters on
    /// fixes that, without changing any C# query code or behavior.
    ///
    /// CONCURRENTLY (+ suppressTransaction) so this never holds a lock on a
    /// live, populated table — each CREATE INDEX runs as its own statement
    /// outside the migration's wrapping transaction.
    /// </summary>
    public partial class AddSearchTrigramIndexes : Migration
    {
        private static readonly (string Table, string Column)[] TrigramTargets =
        [
            ("Members", "FirstName"), ("Members", "LastName"), ("Members", "Company"),
            ("Members", "Email"), ("Members", "JobTitle"), ("Members", "Location"),
            ("BusinessListings", "BusinessName"),
            ("PhotoAlbums", "Title"),
            ("ForumThreads", "Title"),
            ("Resources", "Title"), ("Resources", "Description"),
            ("Jobs", "Title"), ("Jobs", "Company"), ("Jobs", "Location"),
            ("NewsPosts", "Title"), ("NewsPosts", "Content"),
            ("StoreProducts", "Name"),
            ("InstitutionStaff", "FirstName"), ("InstitutionStaff", "LastName"), ("InstitutionStaff", "Email"),
            ("Contributions", "TransactionRef"), ("Contributions", "MemberId"), ("Contributions", "Notes"),
            ("PlatformStaff", "Name"), ("PlatformStaff", "Email"),
            ("Institutions", "Name"), ("Institutions", "Slug"), ("Institutions", "ContactEmail"),
            ("AuditLogEntries", "Actor"), ("AuditLogEntries", "Action"), ("AuditLogEntries", "Target"),
        ];

        private static string IndexName(string table, string column) => $"ix_{table.ToLowerInvariant()}_{column.ToLowerInvariant()}_trgm";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            foreach (var (table, column) in TrigramTargets)
            {
                migrationBuilder.Sql(
                    $"CREATE INDEX CONCURRENTLY IF NOT EXISTS \"{IndexName(table, column)}\" " +
                    $"ON alumni.\"{table}\" USING gin (lower(\"{column}\") gin_trgm_ops);",
                    suppressTransaction: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in TrigramTargets)
            {
                migrationBuilder.Sql(
                    $"DROP INDEX CONCURRENTLY IF EXISTS alumni.\"{IndexName(table, column)}\";",
                    suppressTransaction: true);
            }
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultForumCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-time backfill: every institution that has no forum categories yet gets the starter
            // set (kept as literal SQL so this migration never changes if the C# defaults later do).
            // Institutions that already created their own categories are left exactly as they are.
            migrationBuilder.Sql("""
                INSERT INTO alumni."ForumCategories" ("Id", "InstitutionId", "Name", "Description", "SortOrder", "CreatedAt", "CreatedBy")
                SELECT replace(gen_random_uuid()::text, '-', ''), i."Id", d."Name", d."Description", d."SortOrder", now(), 'system'
                FROM alumni."Institutions" i
                CROSS JOIN (VALUES
                    ('General', 'Open conversation about anything on your mind.', 1),
                    ('Introductions', 'New here? Say hello and tell people a bit about yourself.', 2),
                    ('Careers & Jobs', 'Career advice, job leads and professional questions.', 3),
                    ('Events & Meetups', 'Plan and talk about reunions, meetups and gatherings.', 4),
                    ('Business & Networking', 'Share your business, find partners and make connections.', 5),
                    ('Advice & Support', 'Ask for help, or share what you have learned.', 6)
                ) AS d("Name", "Description", "SortOrder")
                WHERE NOT EXISTS (
                    SELECT 1 FROM alumni."ForumCategories" c WHERE c."InstitutionId" = i."Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

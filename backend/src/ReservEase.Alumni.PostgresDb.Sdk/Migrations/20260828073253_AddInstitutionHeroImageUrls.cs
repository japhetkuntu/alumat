using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionHeroImageUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeroImageUrls",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            // Carry forward each institution's existing single hero photo as the
            // first (only) item in the new list, instead of losing it.
            migrationBuilder.Sql(
                """
                UPDATE alumni."Institutions"
                SET "HeroImageUrls" = to_jsonb(ARRAY["HeroImageUrl"])
                WHERE "HeroImageUrl" IS NOT NULL AND "HeroImageUrl" <> '';
                """);

            migrationBuilder.DropColumn(
                name: "HeroImageUrl",
                schema: "alumni",
                table: "Institutions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeroImageUrl",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE alumni."Institutions"
                SET "HeroImageUrl" = "HeroImageUrls" ->> 0
                WHERE jsonb_array_length("HeroImageUrls") > 0;
                """);

            migrationBuilder.DropColumn(
                name: "HeroImageUrls",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionLandingContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "[]" — a valid empty JSON array, not "" — since existing rows are
            // backfilled with this value and JsonbConverter<T> deserializes it
            // via JsonSerializer, which throws on an empty string.
            migrationBuilder.AddColumn<string>(
                name: "LandingPageStories",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "NewsBanner",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandingPageStories",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "NewsBanner",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

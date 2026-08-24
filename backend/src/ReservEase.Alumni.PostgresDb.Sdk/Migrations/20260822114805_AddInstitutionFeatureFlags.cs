using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "[]" — a valid empty JSON array, not "" — since existing rows are
            // backfilled with this value and JsonbConverter<List<string>> deserializes
            // it via JsonSerializer, which would throw on an empty string.
            migrationBuilder.AddColumn<string>(
                name: "DisabledFeatures",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledFeatures",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

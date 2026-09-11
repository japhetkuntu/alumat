using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramOfStudy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Program",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProgramOfStudyEnabled",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // "[]", not "" — the JsonbConverter deserializes this via JsonSerializer,
            // which throws on an empty string (see DisabledFeatures' own migration).
            migrationBuilder.AddColumn<string>(
                name: "ProgramsOfStudy",
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
                name: "Program",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ProgramOfStudyEnabled",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "ProgramsOfStudy",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

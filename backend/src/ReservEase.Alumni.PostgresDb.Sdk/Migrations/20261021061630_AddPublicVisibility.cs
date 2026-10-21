using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "alumni",
                table: "NewsPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "alumni",
                table: "Jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicProfile",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "alumni",
                table: "Events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "alumni",
                table: "Communities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "alumni",
                table: "NewsPosts");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "alumni",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "PublicProfile",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "alumni",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "alumni",
                table: "Communities");
        }
    }
}

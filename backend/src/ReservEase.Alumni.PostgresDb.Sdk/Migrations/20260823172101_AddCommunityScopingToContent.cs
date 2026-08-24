using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityScopingToContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "Resources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "Events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "ClassNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "Campaigns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "ClassNotes");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "Campaigns");
        }
    }
}

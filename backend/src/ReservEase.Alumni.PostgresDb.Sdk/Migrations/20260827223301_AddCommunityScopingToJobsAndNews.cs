using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityScopingToJobsAndNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "NewsPosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "Jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "NewsPosts");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "Jobs");
        }
    }
}

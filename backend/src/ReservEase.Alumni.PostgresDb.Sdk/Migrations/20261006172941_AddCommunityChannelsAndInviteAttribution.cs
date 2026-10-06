using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityChannelsAndInviteAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "alumni",
                table: "Referrals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "Referrals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalChannels",
                schema: "alumni",
                table: "Communities",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_CommunityId",
                schema: "alumni",
                table: "Referrals",
                column: "CommunityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Referrals_CommunityId",
                schema: "alumni",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "alumni",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "ExternalChannels",
                schema: "alumni",
                table: "Communities");
        }
    }
}

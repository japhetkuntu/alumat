using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignUpdatesAndWallOfSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnWallOfSupport",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnWallOfSupport",
                schema: "alumni",
                table: "Contributions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CampaignUpdates",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    PostedByStaffId = table.Column<string>(type: "text", nullable: true),
                    PostedByName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignUpdates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignUpdates_CampaignId",
                schema: "alumni",
                table: "CampaignUpdates",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignUpdates_InstitutionId",
                schema: "alumni",
                table: "CampaignUpdates",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignUpdates",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "ShowOnWallOfSupport",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ShowOnWallOfSupport",
                schema: "alumni",
                table: "Contributions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarketingAttribution",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketingShareId",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketingAssets",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingCampaigns",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingPosts",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    DestinationPath = table.Column<string>(type: "text", nullable: false),
                    UseLandingPage = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CaptionsJson = table.Column<string>(type: "text", nullable: false),
                    AssetIdsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingShares",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedUrl = table.Column<string>(type: "text", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingShares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingVisits",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ShareId = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingVisits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingLeads_MarketingShareId",
                schema: "alumni",
                table: "OnboardingLeads",
                column: "MarketingShareId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAssets_CampaignId",
                schema: "alumni",
                table: "MarketingAssets",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingPosts_CampaignId",
                schema: "alumni",
                table: "MarketingPosts",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingShares_CampaignId",
                schema: "alumni",
                table: "MarketingShares",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingShares_PostId",
                schema: "alumni",
                table: "MarketingShares",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingVisits_ShareId_SessionId",
                schema: "alumni",
                table: "MarketingVisits",
                columns: new[] { "ShareId", "SessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketingAssets",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MarketingCampaigns",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MarketingPosts",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MarketingShares",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MarketingVisits",
                schema: "alumni");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingLeads_MarketingShareId",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "MarketingAttribution",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "MarketingShareId",
                schema: "alumni",
                table: "OnboardingLeads");
        }
    }
}

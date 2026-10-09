using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityHealthSnapshots",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodDays = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Classification = table.Column<string>(type: "text", nullable: false),
                    ActiveMembers = table.Column<int>(type: "integer", nullable: false),
                    Factors = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EngagementChecklistEntries",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    WeekStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemKey = table.Column<string>(type: "text", nullable: false),
                    CompletedById = table.Column<string>(type: "text", nullable: false),
                    CompletedByName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementChecklistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EngagementRecommendations",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    RuleId = table.Column<string>(type: "text", nullable: false),
                    DedupeKey = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    ActionLabel = table.Column<string>(type: "text", nullable: false),
                    ActionUrl = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SnoozedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<string>(type: "text", nullable: true),
                    ResolvedByName = table.Column<string>(type: "text", nullable: true),
                    AssignedToId = table.Column<string>(type: "text", nullable: true),
                    AssignedToName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementRecommendations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityHealthSnapshots_InstitutionId",
                schema: "alumni",
                table: "CommunityHealthSnapshots",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityHealthSnapshots_InstitutionId_PeriodDays_SnapshotD~",
                schema: "alumni",
                table: "CommunityHealthSnapshots",
                columns: new[] { "InstitutionId", "PeriodDays", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementChecklistEntries_InstitutionId",
                schema: "alumni",
                table: "EngagementChecklistEntries",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementChecklistEntries_InstitutionId_WeekStart_ItemKey",
                schema: "alumni",
                table: "EngagementChecklistEntries",
                columns: new[] { "InstitutionId", "WeekStart", "ItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRecommendations_InstitutionId",
                schema: "alumni",
                table: "EngagementRecommendations",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRecommendations_InstitutionId_DedupeKey",
                schema: "alumni",
                table: "EngagementRecommendations",
                columns: new[] { "InstitutionId", "DedupeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRecommendations_InstitutionId_Status_CreatedAt",
                schema: "alumni",
                table: "EngagementRecommendations",
                columns: new[] { "InstitutionId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityHealthSnapshots",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "EngagementChecklistEntries",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "EngagementRecommendations",
                schema: "alumni");
        }
    }
}

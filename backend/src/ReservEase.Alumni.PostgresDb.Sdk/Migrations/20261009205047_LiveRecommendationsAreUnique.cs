using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class LiveRecommendationsAreUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EngagementRecommendations_InstitutionId_DedupeKey",
                schema: "alumni",
                table: "EngagementRecommendations");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRecommendations_InstitutionId_DedupeKey",
                schema: "alumni",
                table: "EngagementRecommendations",
                columns: new[] { "InstitutionId", "DedupeKey" },
                unique: true,
                filter: "\"Status\" IN ('Open', 'Snoozed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EngagementRecommendations_InstitutionId_DedupeKey",
                schema: "alumni",
                table: "EngagementRecommendations");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementRecommendations_InstitutionId_DedupeKey",
                schema: "alumni",
                table: "EngagementRecommendations",
                columns: new[] { "InstitutionId", "DedupeKey" });
        }
    }
}

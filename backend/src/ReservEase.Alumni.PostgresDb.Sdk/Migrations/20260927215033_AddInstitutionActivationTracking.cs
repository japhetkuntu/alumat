using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionActivationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivationTargetCount",
                schema: "alumni",
                table: "PlatformSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationTargetDate",
                schema: "alumni",
                table: "PlatformSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContactedAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DemoBookedAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialStartedAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                schema: "alumni",
                table: "Institutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivationNudgeSentAt",
                schema: "alumni",
                table: "Institutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstitutionActivitySnapshots",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    WeekStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActiveStaffCount = table.Column<int>(type: "integer", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    MembersEverLoggedIn = table.Column<int>(type: "integer", nullable: false),
                    MembersActiveThisWeek = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulPaymentsThisWeek = table.Column<int>(type: "integer", nullable: false),
                    AmountCollectedThisWeek = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionActivitySnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionActivitySnapshots_InstitutionId_WeekStart",
                schema: "alumni",
                table: "InstitutionActivitySnapshots",
                columns: new[] { "InstitutionId", "WeekStart" },
                unique: true);

            // Backfill funnel timestamps for leads that already moved past New, so the
            // Activation page's funnel doesn't start empty. UpdatedAt is the best record
            // of when the latest status change happened; public-form leads get a Source.
            migrationBuilder.Sql("""
                UPDATE alumni."OnboardingLeads"
                SET "ContactedAt" = COALESCE("UpdatedAt", "CreatedAt")
                WHERE "Status" IN ('Contacted', 'Approved', 'Rejected') AND "ContactedAt" IS NULL;

                UPDATE alumni."OnboardingLeads"
                SET "DemoBookedAt" = COALESCE("UpdatedAt", "CreatedAt"),
                    "TrialStartedAt" = COALESCE("UpdatedAt", "CreatedAt"),
                    "ApprovedAt" = COALESCE("UpdatedAt", "CreatedAt")
                WHERE "Status" = 'Approved' AND "ApprovedAt" IS NULL;

                UPDATE alumni."OnboardingLeads" SET "Source" = 'Website' WHERE "Source" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstitutionActivitySnapshots",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "ActivationTargetCount",
                schema: "alumni",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ActivationTargetDate",
                schema: "alumni",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "ContactedAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "DemoBookedAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "TrialStartedAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "LastActivationNudgeSentAt",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

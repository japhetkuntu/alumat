using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddActivationFollowUpsAndStaffActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivationMilestones",
                schema: "alumni",
                table: "PlatformSettings",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpReminderSentAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivationMinMembers",
                schema: "alumni",
                table: "Institutions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SetupNudgesEnabled",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "StaffActivityWeeks",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    StaffId = table.Column<string>(type: "text", nullable: false),
                    WeekStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffActivityWeeks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingLeads_NextFollowUpAt",
                schema: "alumni",
                table: "OnboardingLeads",
                column: "NextFollowUpAt");

            migrationBuilder.CreateIndex(
                name: "IX_StaffActivityWeeks_InstitutionId_StaffId_WeekStart",
                schema: "alumni",
                table: "StaffActivityWeeks",
                columns: new[] { "InstitutionId", "StaffId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffActivityWeeks_InstitutionId_WeekStart",
                schema: "alumni",
                table: "StaffActivityWeeks",
                columns: new[] { "InstitutionId", "WeekStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffActivityWeeks",
                schema: "alumni");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingLeads_NextFollowUpAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "ActivationMilestones",
                schema: "alumni",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "FollowUpReminderSentAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "NextFollowUpAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "ActivationMinMembers",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "SetupNudgesEnabled",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

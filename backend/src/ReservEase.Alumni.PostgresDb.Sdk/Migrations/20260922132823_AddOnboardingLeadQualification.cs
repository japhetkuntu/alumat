using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingLeadQualification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactRole",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentMemberManagement",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataImportStatus",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationType",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredContactChannel",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredContactTime",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryGoals",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactRole",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "CurrentMemberManagement",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "DataImportStatus",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "OrganizationType",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "PreferredContactChannel",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "PreferredContactTime",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "PrimaryGoals",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "alumni",
                table: "OnboardingLeads");
        }
    }
}

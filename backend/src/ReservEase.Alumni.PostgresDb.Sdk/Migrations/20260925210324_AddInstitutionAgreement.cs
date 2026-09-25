using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionAgreement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgreementAcceptedAt",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementAcceptedByName",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementAcceptedByTitle",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementAcceptedIp",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementVersion",
                schema: "alumni",
                table: "OnboardingLeads",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstitutionAgreementAcceptances",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    AcceptedByStaffId = table.Column<string>(type: "text", nullable: false),
                    AcceptedByName = table.Column<string>(type: "text", nullable: false),
                    AcceptedByEmail = table.Column<string>(type: "text", nullable: false),
                    AcceptedByTitle = table.Column<string>(type: "text", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionAgreementAcceptances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionAgreementAcceptances_InstitutionId",
                schema: "alumni",
                table: "InstitutionAgreementAcceptances",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionAgreementAcceptances_InstitutionId_Version",
                schema: "alumni",
                table: "InstitutionAgreementAcceptances",
                columns: new[] { "InstitutionId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstitutionAgreementAcceptances",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "AgreementAcceptedAt",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "AgreementAcceptedByName",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "AgreementAcceptedByTitle",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "AgreementAcceptedIp",
                schema: "alumni",
                table: "OnboardingLeads");

            migrationBuilder.DropColumn(
                name: "AgreementVersion",
                schema: "alumni",
                table: "OnboardingLeads");
        }
    }
}

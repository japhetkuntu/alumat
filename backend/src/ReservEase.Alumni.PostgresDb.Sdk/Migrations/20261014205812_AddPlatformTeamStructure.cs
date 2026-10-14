using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformTeamStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportsToStaffId",
                schema: "alumni",
                table: "PlatformStaff",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerStaffId",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformStaff_ReportsToStaffId",
                schema: "alumni",
                table: "PlatformStaff",
                column: "ReportsToStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_OwnerStaffId",
                schema: "alumni",
                table: "Institutions",
                column: "OwnerStaffId");

            // Institutions onboarded by platform staff are credited to whoever created them. Anything else stays unassigned,
            // which only SuperAdmins see.
            migrationBuilder.Sql("UPDATE alumni.\"Institutions\" SET \"OwnerStaffId\" = \"CreatedBy\" WHERE \"OwnerStaffId\" IS NULL AND \"CreatedBy\" IN (SELECT \"Id\" FROM alumni.\"PlatformStaff\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformStaff_ReportsToStaffId",
                schema: "alumni",
                table: "PlatformStaff");

            migrationBuilder.DropIndex(
                name: "IX_Institutions_OwnerStaffId",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "ReportsToStaffId",
                schema: "alumni",
                table: "PlatformStaff");

            migrationBuilder.DropColumn(
                name: "OwnerStaffId",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

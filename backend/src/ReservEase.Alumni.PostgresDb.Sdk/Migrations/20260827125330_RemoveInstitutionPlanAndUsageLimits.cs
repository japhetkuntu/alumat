using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInstitutionPlanAndUsageLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MemberLimit",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "Plan",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "StorageLimitGb",
                schema: "alumni",
                table: "Institutions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MemberLimit",
                schema: "alumni",
                table: "Institutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StorageLimitGb",
                schema: "alumni",
                table: "Institutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}

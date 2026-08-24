using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionPortalConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionAuthHeadline",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionAuthSubtext",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionPortalTitle",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberAuthHeadline",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberAuthSubtext",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberPortalTitle",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireStudentId",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconUrl",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "InstitutionAuthHeadline",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "InstitutionAuthSubtext",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "InstitutionPortalTitle",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "MemberAuthHeadline",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "MemberAuthSubtext",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "MemberPortalTitle",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "RequireStudentId",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

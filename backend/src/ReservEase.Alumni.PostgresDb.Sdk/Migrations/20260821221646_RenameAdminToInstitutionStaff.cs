using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminToInstitutionStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A true rename (RenameTable/RenameIndex), not drop+recreate — the
            // scaffolded migration defaulted to DropTable+CreateTable, which
            // would have destroyed every existing institution staff row.
            migrationBuilder.RenameTable(
                name: "Admins",
                schema: "alumni",
                newName: "InstitutionStaff",
                newSchema: "alumni");

            migrationBuilder.RenameIndex(
                schema: "alumni",
                table: "InstitutionStaff",
                name: "IX_Admins_InstitutionId",
                newName: "IX_InstitutionStaff_InstitutionId");

            migrationBuilder.RenameIndex(
                schema: "alumni",
                table: "InstitutionStaff",
                name: "IX_Admins_InstitutionId_Email",
                newName: "IX_InstitutionStaff_InstitutionId_Email");

            migrationBuilder.Sql(
                "ALTER TABLE alumni.\"InstitutionStaff\" RENAME CONSTRAINT \"PK_Admins\" TO \"PK_InstitutionStaff\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE alumni.\"InstitutionStaff\" RENAME CONSTRAINT \"PK_InstitutionStaff\" TO \"PK_Admins\";");

            migrationBuilder.RenameIndex(
                schema: "alumni",
                table: "InstitutionStaff",
                name: "IX_InstitutionStaff_InstitutionId",
                newName: "IX_Admins_InstitutionId");

            migrationBuilder.RenameIndex(
                schema: "alumni",
                table: "InstitutionStaff",
                name: "IX_InstitutionStaff_InstitutionId_Email",
                newName: "IX_Admins_InstitutionId_Email");

            migrationBuilder.RenameTable(
                name: "InstitutionStaff",
                schema: "alumni",
                newName: "Admins",
                newSchema: "alumni");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddGraduationYearMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GraduationYearMode",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: false,
                defaultValue: "Required");

            // Existing communities keep exactly how they behave today: those that were "Community" had no graduation years.
            migrationBuilder.Sql("UPDATE alumni.\"Institutions\" SET \"GraduationYearMode\" = 'Off' WHERE \"OrganizationType\" = 'Community';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraduationYearMode",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

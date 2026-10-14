using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectMemberId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CollectMemberId",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Communities that already have no graduation years never showed the ID field, so it stays off for them.
            migrationBuilder.Sql("UPDATE alumni.\"Institutions\" SET \"CollectMemberId\" = false WHERE \"GraduationYearMode\" = 'Off';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectMemberId",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddAlumniMapCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MapLatitude",
                schema: "alumni",
                table: "Members",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MapLongitude",
                schema: "alumni",
                table: "Members",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapLatitude",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MapLongitude",
                schema: "alumni",
                table: "Members");
        }
    }
}

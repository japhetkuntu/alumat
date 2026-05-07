using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class added_google_map_url : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleLocationUrl",
                schema: "alumni",
                table: "Events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleLocationUrl",
                schema: "alumni",
                table: "Events");
        }
    }
}

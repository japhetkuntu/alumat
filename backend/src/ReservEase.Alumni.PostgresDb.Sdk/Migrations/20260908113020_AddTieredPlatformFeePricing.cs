using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddTieredPlatformFeePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeFlatAmount",
                schema: "alumni",
                table: "Institutions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeFlatThreshold",
                schema: "alumni",
                table: "Institutions",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformFeeFlatAmount",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "PlatformFeeFlatThreshold",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddShowOnWebsite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnWebsite",
                schema: "alumni",
                table: "StoreProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnWebsite",
                schema: "alumni",
                table: "ServiceTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowOnWebsite",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "ShowOnWebsite",
                schema: "alumni",
                table: "ServiceTypes");
        }
    }
}

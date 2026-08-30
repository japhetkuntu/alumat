using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreOrderGatewayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CallbackPayload",
                schema: "alumni",
                table: "StoreOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "alumni",
                table: "StoreOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResponse",
                schema: "alumni",
                table: "StoreOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallbackPayload",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropColumn(
                name: "GatewayResponse",
                schema: "alumni",
                table: "StoreOrders");
        }
    }
}

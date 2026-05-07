using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class updated_contribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Event",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "Member",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.AddColumn<bool>(
                name: "AllowManualPayments",
                schema: "alumni",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowOnlinePayments",
                schema: "alumni",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                schema: "alumni",
                table: "Campaigns",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileMoneyAccount",
                schema: "alumni",
                table: "Campaigns",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowManualPayments",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "AllowOnlinePayments",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "BankAccount",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MobileMoneyAccount",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.AddColumn<string>(
                name: "Event",
                schema: "alumni",
                table: "EventRsvps",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Member",
                schema: "alumni",
                table: "EventRsvps",
                type: "jsonb",
                nullable: true);
        }
    }
}

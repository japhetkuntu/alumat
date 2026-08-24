using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddRevenueModelAndTwoToneBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaystackSubaccountCode",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeePercentage",
                schema: "alumni",
                table: "Institutions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColorHex",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementAccountName",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementAccountNumber",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementBankCode",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementBankName",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmountToInstitution",
                schema: "alumni",
                table: "Contributions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                schema: "alumni",
                table: "Contributions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaystackSubaccountCode",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "PlatformFeePercentage",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "SecondaryColorHex",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "SettlementAccountName",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "SettlementAccountNumber",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "SettlementBankCode",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "SettlementBankName",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "NetAmountToInstitution",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                schema: "alumni",
                table: "Contributions");
        }
    }
}

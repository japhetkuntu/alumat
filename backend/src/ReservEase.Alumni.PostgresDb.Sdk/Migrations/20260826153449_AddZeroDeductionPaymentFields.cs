using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddZeroDeductionPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GatewayFeeAmount",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossChargeAmount",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GatewayFeeAmount",
                schema: "alumni",
                table: "Contributions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossChargeAmount",
                schema: "alumni",
                table: "Contributions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformRevenueAmount",
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
                name: "GatewayFeeAmount",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "GrossChargeAmount",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "GatewayFeeAmount",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "GrossChargeAmount",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "PlatformRevenueAmount",
                schema: "alumni",
                table: "Contributions");
        }
    }
}

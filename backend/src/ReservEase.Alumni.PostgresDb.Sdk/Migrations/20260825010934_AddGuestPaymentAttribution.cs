using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestPaymentAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGuestPayment",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SharedByMemberId",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuestPayment",
                schema: "alumni",
                table: "Contributions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SharedByMemberId",
                schema: "alumni",
                table: "Contributions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGuestPayment",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SharedByMemberId",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "IsGuestPayment",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "SharedByMemberId",
                schema: "alumni",
                table: "Contributions");
        }
    }
}

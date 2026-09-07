using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchPayoutSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutStatus",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "PaystackSubaccountCode",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPayoutChanges",
                schema: "alumni",
                table: "Batches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementAccountName",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementAccountNumber",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementBankCode",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementBankName",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseInstitutionAccount",
                schema: "alumni",
                table: "Batches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_PayoutStatus",
                schema: "alumni",
                table: "Batches",
                column: "PayoutStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Batches_PayoutStatus",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "PayoutStatus",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "PaystackSubaccountCode",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "PendingPayoutChanges",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "SettlementAccountName",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "SettlementAccountNumber",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "SettlementBankCode",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "SettlementBankName",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "UseInstitutionAccount",
                schema: "alumni",
                table: "Batches");
        }
    }
}

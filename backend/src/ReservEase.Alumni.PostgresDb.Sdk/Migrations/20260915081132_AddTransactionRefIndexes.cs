using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionRefIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoreOrders_TransactionRef",
                schema: "alumni",
                table: "StoreOrders",
                column: "TransactionRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_TransactionRef",
                schema: "alumni",
                table: "ServiceRequests",
                column: "TransactionRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoreOrders_TransactionRef",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_TransactionRef",
                schema: "alumni",
                table: "ServiceRequests");
        }
    }
}

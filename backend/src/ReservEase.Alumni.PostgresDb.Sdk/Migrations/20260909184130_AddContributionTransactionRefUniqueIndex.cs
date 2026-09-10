using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionTransactionRefUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_TransactionRef",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_TransactionRef",
                schema: "alumni",
                table: "Contributions",
                column: "TransactionRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_TransactionRef",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_TransactionRef",
                schema: "alumni",
                table: "Contributions",
                column: "TransactionRef");
        }
    }
}

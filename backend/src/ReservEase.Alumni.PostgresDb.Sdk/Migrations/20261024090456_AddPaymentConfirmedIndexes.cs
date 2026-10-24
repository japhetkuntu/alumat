using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentConfirmedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoreOrders_InstitutionId_ConfirmedAt",
                schema: "alumni",
                table: "StoreOrders",
                columns: new[] { "InstitutionId", "ConfirmedAt" },
                filter: "\"Status\" = 'Successful'");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_InstitutionId_ConfirmedAt",
                schema: "alumni",
                table: "ServiceRequests",
                columns: new[] { "InstitutionId", "ConfirmedAt" },
                filter: "\"PaymentStatus\" = 'Successful'");

            migrationBuilder.CreateIndex(
                name: "IX_EventRsvps_InstitutionId_PaidAt",
                schema: "alumni",
                table: "EventRsvps",
                columns: new[] { "InstitutionId", "PaidAt" },
                filter: "\"PaidAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_InstitutionId_ConfirmedAt",
                schema: "alumni",
                table: "Contributions",
                columns: new[] { "InstitutionId", "ConfirmedAt" },
                filter: "\"Status\" = 'Successful'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoreOrders_InstitutionId_ConfirmedAt",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_InstitutionId_ConfirmedAt",
                schema: "alumni",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_EventRsvps_InstitutionId_PaidAt",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_InstitutionId_ConfirmedAt",
                schema: "alumni",
                table: "Contributions");
        }
    }
}

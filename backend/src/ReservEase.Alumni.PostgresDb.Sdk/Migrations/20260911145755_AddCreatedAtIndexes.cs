using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoreOrders_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "StoreOrders",
                columns: new[] { "InstitutionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "ServiceRequests",
                columns: new[] { "InstitutionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Members_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "Members",
                columns: new[] { "InstitutionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "Contributions",
                columns: new[] { "InstitutionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "CommunityMemberships",
                columns: new[] { "InstitutionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoreOrders_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Members_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_CommunityMemberships_InstitutionId_CreatedAt",
                schema: "alumni",
                table: "CommunityMemberships");
        }
    }
}

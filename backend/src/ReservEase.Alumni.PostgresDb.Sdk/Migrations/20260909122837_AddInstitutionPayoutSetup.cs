using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionPayoutSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutStatus",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "PendingPayoutChanges",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: true);

            // Backfill: an institution platform staff already set up directly
            // (PaystackSubaccountCode already live) is already effectively
            // approved — it was just never routed through this submission
            // flow. Everything else correctly starts at "None".
            migrationBuilder.Sql(
                """UPDATE "alumni"."Institutions" SET "PayoutStatus" = 'Approved' WHERE "PaystackSubaccountCode" IS NOT NULL""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutStatus",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "PendingPayoutChanges",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

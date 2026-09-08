using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringGiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SetupRecurringGiving",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecurringContributionId",
                schema: "alumni",
                table: "Contributions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringContributions",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    Campaign = table.Column<string>(type: "jsonb", nullable: true),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    Member = table.Column<string>(type: "jsonb", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AuthorizationCode = table.Column<string>(type: "text", nullable: false),
                    CardLast4 = table.Column<string>(type: "text", nullable: true),
                    CardType = table.Column<string>(type: "text", nullable: true),
                    CardBank = table.Column<string>(type: "text", nullable: true),
                    NextChargeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastChargeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastChargeStatus = table.Column<string>(type: "text", nullable: true),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringContributions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringContributions_InstitutionId",
                schema: "alumni",
                table: "RecurringContributions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringContributions_MemberId",
                schema: "alumni",
                table: "RecurringContributions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringContributions_Status_NextChargeDate",
                schema: "alumni",
                table: "RecurringContributions",
                columns: new[] { "Status", "NextChargeDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringContributions",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "SetupRecurringGiving",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RecurringContributionId",
                schema: "alumni",
                table: "Contributions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPledges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pledges",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    MemberName = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusNote = table.Column<string>(type: "text", nullable: true),
                    UpcomingReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverdueReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pledges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_CampaignId_Status",
                schema: "alumni",
                table: "Pledges",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_InstitutionId",
                schema: "alumni",
                table: "Pledges",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_MemberId_CampaignId",
                schema: "alumni",
                table: "Pledges",
                columns: new[] { "MemberId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_Status_DueDate",
                schema: "alumni",
                table: "Pledges",
                columns: new[] { "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pledges",
                schema: "alumni");
        }
    }
}

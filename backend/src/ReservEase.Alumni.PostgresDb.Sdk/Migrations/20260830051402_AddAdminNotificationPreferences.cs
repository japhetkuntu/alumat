using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminNotificationPreferences",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    StaffId = table.Column<string>(type: "text", nullable: false),
                    PaymentReceivedAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    NewMemberRegistrationAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    PendingApprovalAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    SystemAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotificationPreferences_InstitutionId",
                schema: "alumni",
                table: "AdminNotificationPreferences",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotificationPreferences_StaffId",
                schema: "alumni",
                table: "AdminNotificationPreferences",
                column: "StaffId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminNotificationPreferences",
                schema: "alumni");
        }
    }
}

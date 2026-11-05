using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddIntroductionActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Introductions_RecipientId",
                schema: "alumni",
                table: "Introductions");

            migrationBuilder.DropIndex(
                name: "IX_Introductions_RequesterId",
                schema: "alumni",
                table: "Introductions");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                schema: "alumni",
                table: "Introductions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing conversations: their last activity is the newest thing we already recorded about them.
            migrationBuilder.Sql(@"UPDATE alumni.""Introductions"" SET ""LastActivityAt"" = GREATEST(""CreatedAt"", COALESCE(""LastMessageAt"", ""CreatedAt""), COALESCE(""RespondedAt"", ""CreatedAt""), COALESCE(""UpdatedAt"", ""CreatedAt""));");

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_RecipientId_LastActivityAt",
                schema: "alumni",
                table: "Introductions",
                columns: new[] { "RecipientId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_RequesterId_LastActivityAt",
                schema: "alumni",
                table: "Introductions",
                columns: new[] { "RequesterId", "LastActivityAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Introductions_RecipientId_LastActivityAt",
                schema: "alumni",
                table: "Introductions");

            migrationBuilder.DropIndex(
                name: "IX_Introductions_RequesterId_LastActivityAt",
                schema: "alumni",
                table: "Introductions");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                schema: "alumni",
                table: "Introductions");

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_RecipientId",
                schema: "alumni",
                table: "Introductions",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_RequesterId",
                schema: "alumni",
                table: "Introductions",
                column: "RequesterId");
        }
    }
}

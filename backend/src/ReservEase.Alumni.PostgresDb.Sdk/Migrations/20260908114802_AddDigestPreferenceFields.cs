using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddDigestPreferenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DigestFrequency",
                schema: "alumni",
                table: "NotificationPreferences",
                type: "text",
                nullable: false,
                defaultValue: "Weekly");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDigestSentAt",
                schema: "alumni",
                table: "NotificationPreferences",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigestFrequency",
                schema: "alumni",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "LastDigestSentAt",
                schema: "alumni",
                table: "NotificationPreferences");
        }
    }
}

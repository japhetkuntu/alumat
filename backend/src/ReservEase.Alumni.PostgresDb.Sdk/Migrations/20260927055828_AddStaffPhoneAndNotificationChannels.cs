using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPhoneAndNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "alumni",
                table: "InstitutionStaff",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Channels",
                schema: "alumni",
                table: "Announcements",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "EmailSent",
                schema: "alumni",
                table: "Announcements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SmsSent",
                schema: "alumni",
                table: "Announcements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SmsSkippedNoPhone",
                schema: "alumni",
                table: "Announcements",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "alumni",
                table: "InstitutionStaff");

            migrationBuilder.DropColumn(
                name: "Channels",
                schema: "alumni",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "EmailSent",
                schema: "alumni",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SmsSent",
                schema: "alumni",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SmsSkippedNoPhone",
                schema: "alumni",
                table: "Announcements");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetSentAt",
                schema: "alumni",
                table: "PlatformStaff",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                schema: "alumni",
                table: "PlatformStaff",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetSentAt",
                schema: "alumni",
                table: "InstitutionStaff",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                schema: "alumni",
                table: "InstitutionStaff",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetSentAt",
                schema: "alumni",
                table: "PlatformStaff");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                schema: "alumni",
                table: "PlatformStaff");

            migrationBuilder.DropColumn(
                name: "PasswordResetSentAt",
                schema: "alumni",
                table: "InstitutionStaff");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                schema: "alumni",
                table: "InstitutionStaff");
        }
    }
}

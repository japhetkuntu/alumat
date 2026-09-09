using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdaySpotlight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MemberIds",
                schema: "alumni",
                table: "Spotlights",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Members",
                schema: "alumni",
                table: "Spotlights",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "alumni",
                table: "Spotlights",
                type: "text",
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                schema: "alumni",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MemberIds",
                schema: "alumni",
                table: "Spotlights");

            migrationBuilder.DropColumn(
                name: "Members",
                schema: "alumni",
                table: "Spotlights");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "alumni",
                table: "Spotlights");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "alumni",
                table: "Members");
        }
    }
}

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberCommunityProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionType",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Interests",
                schema: "alumni",
                table: "Members",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowBioOnDirectory",
                schema: "alumni",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowCompanyOnDirectory",
                schema: "alumni",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowEmailOnDirectory",
                schema: "alumni",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPhoneOnDirectory",
                schema: "alumni",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "Skills",
                schema: "alumni",
                table: "Members",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionType",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Interests",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ShowBioOnDirectory",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ShowCompanyOnDirectory",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ShowEmailOnDirectory",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ShowPhoneOnDirectory",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Skills",
                schema: "alumni",
                table: "Members");
        }
    }
}

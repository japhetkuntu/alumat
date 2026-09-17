using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSchoolRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Achievements",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "ClubsAndSocieties",
                schema: "alumni",
                table: "Members",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "House",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "LeadershipRoles",
                schema: "alumni",
                table: "Members",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrefectStatus",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentStatus",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfEntry",
                schema: "alumni",
                table: "Members",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Achievements",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ClubsAndSocieties",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "House",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "LeadershipRoles",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PrefectStatus",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "StudentStatus",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "YearOfEntry",
                schema: "alumni",
                table: "Members");
        }
    }
}

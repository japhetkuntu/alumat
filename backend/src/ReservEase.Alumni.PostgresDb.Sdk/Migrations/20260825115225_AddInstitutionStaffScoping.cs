using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionStaffScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunityIds",
                schema: "alumni",
                table: "InstitutionStaff",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "YearGroups",
                schema: "alumni",
                table: "InstitutionStaff",
                type: "integer[]",
                nullable: true);

            // Preserve any existing single-year scoping by carrying it into the new list column.
            migrationBuilder.Sql(
                "UPDATE alumni.\"InstitutionStaff\" SET \"YearGroups\" = ARRAY[\"YearGroup\"] WHERE \"YearGroup\" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "YearGroup",
                schema: "alumni",
                table: "InstitutionStaff");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityIds",
                schema: "alumni",
                table: "InstitutionStaff");

            migrationBuilder.DropColumn(
                name: "YearGroups",
                schema: "alumni",
                table: "InstitutionStaff");

            migrationBuilder.AddColumn<int>(
                name: "YearGroup",
                schema: "alumni",
                table: "InstitutionStaff",
                type: "integer",
                nullable: true);
        }
    }
}

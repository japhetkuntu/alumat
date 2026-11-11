using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteFeaturing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeaturedAt",
                schema: "alumni",
                table: "Institutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedBlurb",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedBy",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FeaturedOnWebsite",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FeaturedOrder",
                schema: "alumni",
                table: "Institutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedStats",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_FeaturedOnWebsite",
                schema: "alumni",
                table: "Institutions",
                column: "FeaturedOnWebsite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Institutions_FeaturedOnWebsite",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "FeaturedAt",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "FeaturedBlurb",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "FeaturedBy",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "FeaturedOnWebsite",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "FeaturedOrder",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "FeaturedStats",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class added_compensation_to_campaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaystackDisbursed",
                schema: "alumni",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaystackDisbursedAt",
                schema: "alumni",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackDisbursedBy",
                schema: "alumni",
                table: "Campaigns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaystackDisbursed",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaystackDisbursedAt",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaystackDisbursedBy",
                schema: "alumni",
                table: "Campaigns");
        }
    }
}

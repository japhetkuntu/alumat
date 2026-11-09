using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutDecisionRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutDecidedAt",
                schema: "alumni",
                table: "Institutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDecidedBy",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDecisionNote",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutDecidedAt",
                schema: "alumni",
                table: "Communities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDecidedBy",
                schema: "alumni",
                table: "Communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDecisionNote",
                schema: "alumni",
                table: "Communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutDecidedAt",
                schema: "alumni",
                table: "Batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDecidedBy",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDecisionNote",
                schema: "alumni",
                table: "Batches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutDecidedAt",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "PayoutDecidedBy",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "PayoutDecisionNote",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "PayoutDecidedAt",
                schema: "alumni",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "PayoutDecidedBy",
                schema: "alumni",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "PayoutDecisionNote",
                schema: "alumni",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "PayoutDecidedAt",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "PayoutDecidedBy",
                schema: "alumni",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "PayoutDecisionNote",
                schema: "alumni",
                table: "Batches");
        }
    }
}

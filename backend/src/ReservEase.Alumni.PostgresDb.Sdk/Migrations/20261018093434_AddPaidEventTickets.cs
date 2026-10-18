using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidEventTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PaidEventsEnabled",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                schema: "alumni",
                table: "EventRsvps",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackPayload",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GatewayFeeAmount",
                schema: "alumni",
                table: "EventRsvps",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResponse",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossChargeAmount",
                schema: "alumni",
                table: "EventRsvps",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                schema: "alumni",
                table: "EventRsvps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                schema: "alumni",
                table: "EventRsvps",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionChargeAmount",
                schema: "alumni",
                table: "EventRsvps",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TransactionRef",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventRsvps_TransactionRef",
                schema: "alumni",
                table: "EventRsvps",
                column: "TransactionRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventRsvps_TransactionRef",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "PaidEventsEnabled",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "CallbackPayload",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "GatewayFeeAmount",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "GatewayResponse",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "GrossChargeAmount",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "TransactionChargeAmount",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "TransactionRef",
                schema: "alumni",
                table: "EventRsvps");
        }
    }
}

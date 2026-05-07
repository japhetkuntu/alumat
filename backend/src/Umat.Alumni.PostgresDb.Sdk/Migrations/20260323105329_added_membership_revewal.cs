using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class added_membership_revewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MembershipYears",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMembershipActive",
                schema: "alumni",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMembershipPaidAt",
                schema: "alumni",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MembershipExpiry",
                schema: "alumni",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MembershipYearsPaid",
                schema: "alumni",
                table: "Members",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsMembershipCampaign",
                schema: "alumni",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MembershipYears",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "IsMembershipActive",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "LastMembershipPaidAt",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MembershipExpiry",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MembershipYearsPaid",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "IsMembershipCampaign",
                schema: "alumni",
                table: "Campaigns");
        }
    }
}

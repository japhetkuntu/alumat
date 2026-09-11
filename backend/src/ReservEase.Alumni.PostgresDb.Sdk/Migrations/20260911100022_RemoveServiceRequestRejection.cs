using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServiceRequestRejection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRejected",
                schema: "alumni",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "alumni",
                table: "ServiceRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                schema: "alumni",
                table: "ServiceRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "alumni",
                table: "ServiceRequests",
                type: "text",
                nullable: true);
        }
    }
}

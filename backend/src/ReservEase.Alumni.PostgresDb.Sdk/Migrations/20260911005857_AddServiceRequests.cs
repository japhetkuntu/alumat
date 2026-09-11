using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    RequestNumber = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    Member = table.Column<string>(type: "jsonb", nullable: true),
                    ServiceTypeId = table.Column<string>(type: "text", nullable: false),
                    ServiceTypeName = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    FieldAnswers = table.Column<string>(type: "jsonb", nullable: false),
                    Attachments = table.Column<string>(type: "jsonb", nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    TransactionRef = table.Column<string>(type: "text", nullable: true),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    GatewayFeeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TransactionChargeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossChargeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureMessage = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<string>(type: "text", nullable: true),
                    GatewayResponse = table.Column<string>(type: "text", nullable: true),
                    CallbackPayload = table.Column<string>(type: "text", nullable: true),
                    CurrentStage = table.Column<string>(type: "text", nullable: false),
                    IsRejected = table.Column<bool>(type: "boolean", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    Updates = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Fields = table.Column<string>(type: "jsonb", nullable: false),
                    Stages = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_InstitutionId",
                schema: "alumni",
                table: "ServiceRequests",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_MemberId",
                schema: "alumni",
                table: "ServiceRequests",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ServiceTypeId",
                schema: "alumni",
                table: "ServiceRequests",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTypes_InstitutionId",
                schema: "alumni",
                table: "ServiceTypes",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRequests",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "ServiceTypes",
                schema: "alumni");
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddSpotlightFeaturingAndInstitutionAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ForumThreadIds",
                schema: "alumni",
                table: "Spotlights",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                schema: "alumni",
                table: "Spotlights",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InstitutionAuditLogEntries",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: true),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionAuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionAuditLogEntries_InstitutionId",
                schema: "alumni",
                table: "InstitutionAuditLogEntries",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstitutionAuditLogEntries",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "ForumThreadIds",
                schema: "alumni",
                table: "Spotlights");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                schema: "alumni",
                table: "Spotlights");
        }
    }
}

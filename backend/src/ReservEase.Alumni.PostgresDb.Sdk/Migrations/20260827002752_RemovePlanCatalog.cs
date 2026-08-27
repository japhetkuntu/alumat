using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlanCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plans",
                schema: "alumni");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plans",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BillingInterval = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    IsMostUsed = table.Column<bool>(type: "boolean", nullable: false),
                    MemberLimit = table.Column<int>(type: "integer", nullable: true),
                    Modules = table.Column<string>(type: "jsonb", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    StorageLimitGb = table.Column<int>(type: "integer", nullable: true),
                    SupportLevel = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Name",
                schema: "alumni",
                table: "Plans",
                column: "Name",
                unique: true);
        }
    }
}

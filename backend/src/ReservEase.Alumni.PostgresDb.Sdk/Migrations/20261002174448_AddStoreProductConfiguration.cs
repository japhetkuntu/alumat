using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreProductConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryFields",
                schema: "alumni",
                table: "StoreProducts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                schema: "alumni",
                table: "StoreProducts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Fields",
                schema: "alumni",
                table: "StoreProducts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "PriceLabel",
                schema: "alumni",
                table: "StoreProducts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stages",
                schema: "alumni",
                table: "StoreProducts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "TrackStock",
                schema: "alumni",
                table: "StoreProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "StoreProductTemplates",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PriceLabel = table.Column<string>(type: "text", nullable: true),
                    TrackStock = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryInfo = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: false),
                    Fields = table.Column<string>(type: "jsonb", nullable: false),
                    DeliveryFields = table.Column<string>(type: "jsonb", nullable: false),
                    Stages = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProductTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductTemplates_InstitutionId",
                schema: "alumni",
                table: "StoreProductTemplates",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreProductTemplates",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "DeliveryFields",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "Details",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "Fields",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "PriceLabel",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "Stages",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "TrackStock",
                schema: "alumni",
                table: "StoreProducts");
        }
    }
}

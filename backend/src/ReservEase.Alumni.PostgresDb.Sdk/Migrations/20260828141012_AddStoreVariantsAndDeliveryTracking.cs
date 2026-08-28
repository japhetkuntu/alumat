using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreVariantsAndDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VariantOptionTypes",
                schema: "alumni",
                table: "StoreProducts",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                schema: "alumni",
                table: "StoreOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatusHistory",
                schema: "alumni",
                table: "StoreOrders",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryStatusUpdatedAt",
                schema: "alumni",
                table: "StoreOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                schema: "alumni",
                table: "StoreOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoreDeliveryStages",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "StoreProductVariants",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    PriceOverride = table.Column<decimal>(type: "numeric", nullable: true),
                    QuantityAvailable = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProductVariants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductVariants_InstitutionId",
                schema: "alumni",
                table: "StoreProductVariants",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreProductVariants_ProductId",
                schema: "alumni",
                table: "StoreProductVariants",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreProductVariants",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "VariantOptionTypes",
                schema: "alumni",
                table: "StoreProducts");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusHistory",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusUpdatedAt",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                schema: "alumni",
                table: "StoreOrders");

            migrationBuilder.DropColumn(
                name: "StoreDeliveryStages",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

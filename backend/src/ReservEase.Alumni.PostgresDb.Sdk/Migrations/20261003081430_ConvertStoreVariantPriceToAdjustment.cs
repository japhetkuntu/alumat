using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class ConvertStoreVariantPriceToAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceAdjustment",
                schema: "alumni",
                table: "StoreProductVariants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // A variant used to carry its own full price (or inherit the product's), with the product's
            // price rolled down to the cheapest option. It now carries only the extra on top of the
            // product's price, so every existing option keeps selling at exactly the same price.
            migrationBuilder.Sql(@"
                UPDATE alumni.""StoreProductVariants"" v
                SET ""PriceAdjustment"" = v.""PriceOverride"" - p.""Price""
                FROM alumni.""StoreProducts"" p
                WHERE p.""Id"" = v.""ProductId"" AND v.""PriceOverride"" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "PriceOverride",
                schema: "alumni",
                table: "StoreProductVariants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceOverride",
                schema: "alumni",
                table: "StoreProductVariants",
                type: "numeric",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE alumni.""StoreProductVariants"" v
                SET ""PriceOverride"" = p.""Price"" + v.""PriceAdjustment""
                FROM alumni.""StoreProducts"" p
                WHERE p.""Id"" = v.""ProductId"" AND v.""PriceAdjustment"" <> 0;");

            migrationBuilder.DropColumn(
                name: "PriceAdjustment",
                schema: "alumni",
                table: "StoreProductVariants");
        }
    }
}

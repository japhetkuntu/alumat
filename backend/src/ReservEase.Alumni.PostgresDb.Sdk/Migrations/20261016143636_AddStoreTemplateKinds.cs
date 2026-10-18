using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreTemplateKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "alumni",
                table: "StoreProductTemplates",
                type: "text",
                nullable: false,
                defaultValue: "Setup");

            migrationBuilder.AddColumn<string>(
                name: "VariantOptions",
                schema: "alumni",
                table: "StoreProductTemplates",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "alumni",
                table: "StoreProductTemplates");

            migrationBuilder.DropColumn(
                name: "VariantOptions",
                schema: "alumni",
                table: "StoreProductTemplates");
        }
    }
}

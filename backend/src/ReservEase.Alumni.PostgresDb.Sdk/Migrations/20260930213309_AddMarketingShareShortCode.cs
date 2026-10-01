using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingShareShortCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortCode",
                schema: "alumni",
                table: "MarketingShares",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingShares_ShortCode",
                schema: "alumni",
                table: "MarketingShares",
                column: "ShortCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketingShares_ShortCode",
                schema: "alumni",
                table: "MarketingShares");

            migrationBuilder.DropColumn(
                name: "ShortCode",
                schema: "alumni",
                table: "MarketingShares");
        }
    }
}

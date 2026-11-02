using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPriceOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PriceOptions",
                schema: "alumni",
                table: "Events",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "PriceOptionId",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceOptionName",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceOptions",
                schema: "alumni",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PriceOptionId",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "PriceOptionName",
                schema: "alumni",
                table: "EventRsvps");
        }
    }
}

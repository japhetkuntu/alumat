using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastChannelGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BroadcastEmailAllowed",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BroadcastSmsAllowed",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BroadcastWhatsAppAllowed",
                schema: "alumni",
                table: "Institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Institutions that exist today already broadcast by email and SMS, so they keep that: this change must not
            // silently take it away. Only institutions created from now on start with these off. Platform staff can
            // switch any institution off from its page. (WhatsApp broadcast does not exist yet, so it stays off.)
            migrationBuilder.Sql(@"UPDATE alumni.""Institutions"" SET ""BroadcastEmailAllowed"" = TRUE, ""BroadcastSmsAllowed"" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BroadcastEmailAllowed",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "BroadcastSmsAllowed",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "BroadcastWhatsAppAllowed",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

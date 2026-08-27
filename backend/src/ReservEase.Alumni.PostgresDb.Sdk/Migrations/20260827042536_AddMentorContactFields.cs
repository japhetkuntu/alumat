using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactLinkedInUrl",
                schema: "alumni",
                table: "MentorProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhoneNumber",
                schema: "alumni",
                table: "MentorProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactWhatsAppNumber",
                schema: "alumni",
                table: "MentorProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactLinkedInUrl",
                schema: "alumni",
                table: "MentorProfiles");

            migrationBuilder.DropColumn(
                name: "ContactPhoneNumber",
                schema: "alumni",
                table: "MentorProfiles");

            migrationBuilder.DropColumn(
                name: "ContactWhatsAppNumber",
                schema: "alumni",
                table: "MentorProfiles");
        }
    }
}

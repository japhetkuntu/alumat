using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupKindAndRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFields",
                schema: "alumni",
                table: "Members",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationFields",
                schema: "alumni",
                table: "Institutions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "alumni",
                table: "Communities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "RegistrationFields",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "alumni",
                table: "Communities");
        }
    }
}

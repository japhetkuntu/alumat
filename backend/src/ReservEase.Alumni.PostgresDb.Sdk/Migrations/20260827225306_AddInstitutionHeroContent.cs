using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionHeroContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeroHeadline",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeroImageUrl",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeroHeadline",
                schema: "alumni",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "HeroImageUrl",
                schema: "alumni",
                table: "Institutions");
        }
    }
}

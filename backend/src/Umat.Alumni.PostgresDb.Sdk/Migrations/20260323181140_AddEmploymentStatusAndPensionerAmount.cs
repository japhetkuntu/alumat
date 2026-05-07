using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentStatusAndPensionerAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmploymentStatus",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "Employed");

            migrationBuilder.AddColumn<decimal>(
                name: "PensionerAmountPerMember",
                schema: "alumni",
                table: "Campaigns",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PensionerAmountPerMember",
                schema: "alumni",
                table: "Campaigns");
        }
    }
}

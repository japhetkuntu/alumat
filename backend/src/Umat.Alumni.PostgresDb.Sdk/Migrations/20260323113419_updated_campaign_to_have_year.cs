using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class updated_campaign_to_have_year : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MembershipYear",
                schema: "alumni",
                table: "Campaigns",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MembershipYear",
                schema: "alumni",
                table: "Campaigns");
        }
    }
}

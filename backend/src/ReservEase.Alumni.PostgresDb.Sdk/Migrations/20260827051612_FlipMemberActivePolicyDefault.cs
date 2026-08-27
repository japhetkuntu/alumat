using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class FlipMemberActivePolicyDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only changes the column's DEFAULT for future inserts — existing
            // institution rows keep whatever value they already have (their
            // actual current configuration), not backfilled.
            migrationBuilder.AlterColumn<string>(
                name: "MemberActivePolicy",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: false,
                defaultValue: "ApprovedOnly",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "DuesRequired");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MemberActivePolicy",
                schema: "alumni",
                table: "Institutions",
                type: "text",
                nullable: false,
                defaultValue: "DuesRequired",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "ApprovedOnly");
        }
    }
}

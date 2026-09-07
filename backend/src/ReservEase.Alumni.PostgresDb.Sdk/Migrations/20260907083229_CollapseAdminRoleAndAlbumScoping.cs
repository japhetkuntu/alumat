using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class CollapseAdminRoleAndAlbumScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "PhotoAlbums",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "YearGroups",
                schema: "alumni",
                table: "PhotoAlbums",
                type: "integer[]",
                nullable: true);

            // Institution staff roles collapsed from three (SuperAdmin, Admin,
            // ScopedAdmin) to two (SuperAdmin, ScopedAdmin) — Admin and
            // SuperAdmin were already functionally identical everywhere except
            // staff management/store/payouts (already SuperAdmin-only), so
            // folding existing Admin staff into SuperAdmin is a permission
            // increase on those three areas, matching "SuperAdmin does
            // everything" — not a change to what they could already do
            // elsewhere.
            migrationBuilder.Sql(
                "UPDATE alumni.\"InstitutionStaff\" SET \"Role\" = 'SuperAdmin' WHERE \"Role\" = 'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "PhotoAlbums");

            migrationBuilder.DropColumn(
                name: "YearGroups",
                schema: "alumni",
                table: "PhotoAlbums");

            // Deliberately no reverse data migration — there's no way to tell
            // which former-SuperAdmin rows used to be "Admin" vs. genuinely
            // "SuperAdmin", so rolling back the schema leaves everyone as
            // SuperAdmin rather than guessing.
        }
    }
}

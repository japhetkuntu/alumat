using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyInstitutionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Institution.Status collapses to just "Active"/"Suspended" — a
            // trial institution was still fully functional, so it maps to
            // Active; a cancelled one was already effectively unusable, so
            // it maps to the new Suspended (which now actually enforces a
            // lockout, unlike the old unused "Cancelled" value).
            migrationBuilder.Sql("UPDATE \"Institutions\" SET \"Status\" = 'Active' WHERE \"Status\" = 'Trial';");
            migrationBuilder.Sql("UPDATE \"Institutions\" SET \"Status\" = 'Suspended' WHERE \"Status\" = 'Cancelled';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible — original Trial/Cancelled vs Active/Suspended
            // distinction is lost once collapsed.
        }
    }
}

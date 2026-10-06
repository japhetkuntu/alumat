using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignAllowPledges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowPledges",
                schema: "alumni",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Pledging used to be open on every fundraiser. Leave it on where members have already pledged, so those pledges,
            // their reminders and the admin's list keep working; every other fundraiser starts with it off.
            migrationBuilder.Sql(
                "UPDATE alumni.\"Campaigns\" SET \"AllowPledges\" = TRUE " +
                "WHERE \"IsMembershipCampaign\" = FALSE AND \"Id\" IN (SELECT DISTINCT \"CampaignId\" FROM alumni.\"Pledges\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowPledges",
                schema: "alumni",
                table: "Campaigns");
        }
    }
}

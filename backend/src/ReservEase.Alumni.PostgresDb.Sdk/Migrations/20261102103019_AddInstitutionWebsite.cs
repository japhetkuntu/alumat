using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionWebsite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstitutionWebsites",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    DraftRevision = table.Column<int>(type: "integer", nullable: false),
                    PublishedJson = table.Column<string>(type: "text", nullable: true),
                    PublishedRevision = table.Column<int>(type: "integer", nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionWebsites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionWebsiteVersions",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    PublishedByName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionWebsiteVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionWebsites_InstitutionId",
                schema: "alumni",
                table: "InstitutionWebsites",
                column: "InstitutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionWebsiteVersions_InstitutionId",
                schema: "alumni",
                table: "InstitutionWebsiteVersions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionWebsiteVersions_InstitutionId_Revision",
                schema: "alumni",
                table: "InstitutionWebsiteVersions",
                columns: new[] { "InstitutionId", "Revision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstitutionWebsites",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "InstitutionWebsiteVersions",
                schema: "alumni");
        }
    }
}

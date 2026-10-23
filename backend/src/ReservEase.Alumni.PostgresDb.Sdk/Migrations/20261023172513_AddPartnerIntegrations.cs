using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Serves the case-insensitive student-ID lookup of the partner status check (lower("StudentId") = ...).
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Members_InstitutionId_StudentIdLower\" ON alumni.\"Members\" (\"InstitutionId\", lower(\"StudentId\"))");

            migrationBuilder.AddColumn<DateTime>(
                name: "IntegrationBannerUntil",
                schema: "alumni",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardingLinkId",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstitutionIntegrations",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PortalName = table.Column<string>(type: "text", nullable: false),
                    RedirectUrl = table.Column<string>(type: "text", nullable: false),
                    BannerMessage = table.Column<string>(type: "text", nullable: false),
                    BannerHours = table.Column<int>(type: "integer", nullable: false),
                    LinkExpiryHours = table.Column<int>(type: "integer", nullable: false),
                    StatusCacheDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionIntegrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationApiKeys",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    KeyPrefix = table.Column<string>(type: "text", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingLinks",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MemberId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_InstitutionId_StudentId",
                schema: "alumni",
                table: "Members",
                columns: new[] { "InstitutionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionIntegrations_InstitutionId",
                schema: "alumni",
                table: "InstitutionIntegrations",
                column: "InstitutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationApiKeys_InstitutionId",
                schema: "alumni",
                table: "IntegrationApiKeys",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationApiKeys_KeyHash",
                schema: "alumni",
                table: "IntegrationApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingLinks_InstitutionId",
                schema: "alumni",
                table: "OnboardingLinks",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingLinks_InstitutionId_StudentId",
                schema: "alumni",
                table: "OnboardingLinks",
                columns: new[] { "InstitutionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingLinks_TokenHash",
                schema: "alumni",
                table: "OnboardingLinks",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS alumni.\"IX_Members_InstitutionId_StudentIdLower\"");

            migrationBuilder.DropTable(
                name: "InstitutionIntegrations",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "IntegrationApiKeys",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "OnboardingLinks",
                schema: "alumni");

            migrationBuilder.DropIndex(
                name: "IX_Members_InstitutionId_StudentId",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "IntegrationBannerUntil",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "OnboardingLinkId",
                schema: "alumni",
                table: "Members");
        }
    }
}

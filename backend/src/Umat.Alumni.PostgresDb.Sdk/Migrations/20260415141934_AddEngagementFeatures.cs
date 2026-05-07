using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferredById",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassNoteLikes",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ClassNoteId = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassNoteLikes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassNotes",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "jsonb", nullable: true),
                    YearGroup = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberBadges",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    Member = table.Column<string>(type: "jsonb", nullable: true),
                    BadgeType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberBadges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    MembershipReminders = table.Column<bool>(type: "boolean", nullable: false),
                    CampaignAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    EventReminders = table.Column<bool>(type: "boolean", nullable: false),
                    JobAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    ClassNoteAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    SpotlightAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ReferrerId = table.Column<string>(type: "text", nullable: false),
                    Referrer = table.Column<string>(type: "jsonb", nullable: true),
                    ReferredEmail = table.Column<string>(type: "text", nullable: false),
                    ReferredMemberId = table.Column<string>(type: "text", nullable: true),
                    ReferredMember = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spotlights",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    Member = table.Column<string>(type: "jsonb", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Story = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FeaturedMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spotlights", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassNoteLikes_ClassNoteId",
                schema: "alumni",
                table: "ClassNoteLikes",
                column: "ClassNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassNoteLikes_ClassNoteId_MemberId",
                schema: "alumni",
                table: "ClassNoteLikes",
                columns: new[] { "ClassNoteId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassNotes_AuthorId",
                schema: "alumni",
                table: "ClassNotes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassNotes_YearGroup",
                schema: "alumni",
                table: "ClassNotes",
                column: "YearGroup");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_BadgeType",
                schema: "alumni",
                table: "MemberBadges",
                column: "BadgeType");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_MemberId",
                schema: "alumni",
                table: "MemberBadges",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_MemberId",
                schema: "alumni",
                table: "NotificationPreferences",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredEmail",
                schema: "alumni",
                table: "Referrals",
                column: "ReferredEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerId",
                schema: "alumni",
                table: "Referrals",
                column: "ReferrerId");

            migrationBuilder.CreateIndex(
                name: "IX_Spotlights_MemberId",
                schema: "alumni",
                table: "Spotlights",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Spotlights_Status",
                schema: "alumni",
                table: "Spotlights",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassNoteLikes",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "ClassNotes",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MemberBadges",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "Referrals",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "Spotlights",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ReferredById",
                schema: "alumni",
                table: "Members");
        }
    }
}

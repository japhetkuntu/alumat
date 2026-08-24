using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionAndTenantScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_Email",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Admins_Email",
                schema: "alumni",
                table: "Admins");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Spotlights",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Resources",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Referrals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "PaymentTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Notifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "NotificationPreferences",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "NewsPosts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "MentorshipRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "MentorProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "MemberBadges",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Jobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "ForumThreads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "ForumPosts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "ForumCategories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "EventRsvps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Departments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Contributions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "ClassNotes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "ClassNoteLikes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                schema: "alumni",
                table: "Admins",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Institutions",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    CustomDomain = table.Column<string>(type: "text", nullable: true),
                    PortalName = table.Column<string>(type: "text", nullable: false),
                    Tagline = table.Column<string>(type: "text", nullable: true),
                    ContactName = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    SupportEmail = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    PrimaryColorHex = table.Column<string>(type: "text", nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MemberLimit = table.Column<int>(type: "integer", nullable: false),
                    StorageLimitGb = table.Column<int>(type: "integer", nullable: false),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnboardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformStaff",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Team = table.Column<string>(type: "text", nullable: true),
                    Mfa = table.Column<bool>(type: "boolean", nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformStaff", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spotlights_InstitutionId",
                schema: "alumni",
                table: "Spotlights",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_InstitutionId",
                schema: "alumni",
                table: "Resources",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_InstitutionId",
                schema: "alumni",
                table: "Referrals",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InstitutionId",
                schema: "alumni",
                table: "PaymentTransactions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_InstitutionId",
                schema: "alumni",
                table: "Notifications",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_InstitutionId",
                schema: "alumni",
                table: "NotificationPreferences",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_InstitutionId",
                schema: "alumni",
                table: "NewsPosts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipRequests_InstitutionId",
                schema: "alumni",
                table: "MentorshipRequests",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorProfiles_InstitutionId",
                schema: "alumni",
                table: "MentorProfiles",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_InstitutionId",
                schema: "alumni",
                table: "Members",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_InstitutionId_Email",
                schema: "alumni",
                table: "Members",
                columns: new[] { "InstitutionId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_InstitutionId",
                schema: "alumni",
                table: "MemberBadges",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_InstitutionId",
                schema: "alumni",
                table: "Jobs",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumThreads_InstitutionId",
                schema: "alumni",
                table: "ForumThreads",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_InstitutionId",
                schema: "alumni",
                table: "ForumPosts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumCategories_InstitutionId",
                schema: "alumni",
                table: "ForumCategories",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_InstitutionId",
                schema: "alumni",
                table: "Events",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventRsvps_InstitutionId",
                schema: "alumni",
                table: "EventRsvps",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_InstitutionId",
                schema: "alumni",
                table: "Departments",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_InstitutionId",
                schema: "alumni",
                table: "Contributions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassNotes_InstitutionId",
                schema: "alumni",
                table: "ClassNotes",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassNoteLikes_InstitutionId",
                schema: "alumni",
                table: "ClassNoteLikes",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_InstitutionId",
                schema: "alumni",
                table: "Campaigns",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_InstitutionId",
                schema: "alumni",
                table: "Admins",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_InstitutionId_Email",
                schema: "alumni",
                table: "Admins",
                columns: new[] { "InstitutionId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_CustomDomain",
                schema: "alumni",
                table: "Institutions",
                column: "CustomDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_Slug",
                schema: "alumni",
                table: "Institutions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_Status",
                schema: "alumni",
                table: "Institutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformStaff_Email",
                schema: "alumni",
                table: "PlatformStaff",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Institutions",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "PlatformStaff",
                schema: "alumni");

            migrationBuilder.DropIndex(
                name: "IX_Spotlights_InstitutionId",
                schema: "alumni",
                table: "Spotlights");

            migrationBuilder.DropIndex(
                name: "IX_Resources_InstitutionId",
                schema: "alumni",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_InstitutionId",
                schema: "alumni",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_InstitutionId",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_InstitutionId",
                schema: "alumni",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_InstitutionId",
                schema: "alumni",
                table: "NotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_NewsPosts_InstitutionId",
                schema: "alumni",
                table: "NewsPosts");

            migrationBuilder.DropIndex(
                name: "IX_MentorshipRequests_InstitutionId",
                schema: "alumni",
                table: "MentorshipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MentorProfiles_InstitutionId",
                schema: "alumni",
                table: "MentorProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Members_InstitutionId",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Members_InstitutionId_Email",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_MemberBadges_InstitutionId",
                schema: "alumni",
                table: "MemberBadges");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_InstitutionId",
                schema: "alumni",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_ForumThreads_InstitutionId",
                schema: "alumni",
                table: "ForumThreads");

            migrationBuilder.DropIndex(
                name: "IX_ForumPosts_InstitutionId",
                schema: "alumni",
                table: "ForumPosts");

            migrationBuilder.DropIndex(
                name: "IX_ForumCategories_InstitutionId",
                schema: "alumni",
                table: "ForumCategories");

            migrationBuilder.DropIndex(
                name: "IX_Events_InstitutionId",
                schema: "alumni",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_EventRsvps_InstitutionId",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropIndex(
                name: "IX_Departments_InstitutionId",
                schema: "alumni",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_InstitutionId",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_ClassNotes_InstitutionId",
                schema: "alumni",
                table: "ClassNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClassNoteLikes_InstitutionId",
                schema: "alumni",
                table: "ClassNoteLikes");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_InstitutionId",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Admins_InstitutionId",
                schema: "alumni",
                table: "Admins");

            migrationBuilder.DropIndex(
                name: "IX_Admins_InstitutionId_Email",
                schema: "alumni",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Spotlights");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "NewsPosts");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "MentorshipRequests");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "MentorProfiles");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "MemberBadges");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "ForumThreads");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "ForumCategories");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "ClassNotes");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "ClassNoteLikes");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                schema: "alumni",
                table: "Admins");

            migrationBuilder.CreateIndex(
                name: "IX_Members_Email",
                schema: "alumni",
                table: "Members",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Email",
                schema: "alumni",
                table: "Admins",
                column: "Email",
                unique: true);
        }
    }
}

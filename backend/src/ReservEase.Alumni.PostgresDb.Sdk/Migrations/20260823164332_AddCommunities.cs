using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunityId",
                schema: "alumni",
                table: "ForumThreads",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Communities",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Communities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityMemberships",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMemberships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForumThreads_CommunityId",
                schema: "alumni",
                table: "ForumThreads",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_InstitutionId",
                schema: "alumni",
                table: "Communities",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_CommunityId_MemberId",
                schema: "alumni",
                table: "CommunityMemberships",
                columns: new[] { "CommunityId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_InstitutionId",
                schema: "alumni",
                table: "CommunityMemberships",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_MemberId",
                schema: "alumni",
                table: "CommunityMemberships",
                column: "MemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Communities",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "CommunityMemberships",
                schema: "alumni");

            migrationBuilder.DropIndex(
                name: "IX_ForumThreads_CommunityId",
                schema: "alumni",
                table: "ForumThreads");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "alumni",
                table: "ForumThreads");
        }
    }
}

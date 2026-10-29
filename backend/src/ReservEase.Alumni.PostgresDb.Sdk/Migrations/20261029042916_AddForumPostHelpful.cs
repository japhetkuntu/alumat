using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddForumPostHelpful : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForumPostHelpfuls",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<string>(type: "text", nullable: false),
                    ThreadId = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPostHelpfuls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostHelpfuls_InstitutionId",
                schema: "alumni",
                table: "ForumPostHelpfuls",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostHelpfuls_PostId_MemberId",
                schema: "alumni",
                table: "ForumPostHelpfuls",
                columns: new[] { "PostId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostHelpfuls_ThreadId",
                schema: "alumni",
                table: "ForumPostHelpfuls",
                column: "ThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForumPostHelpfuls",
                schema: "alumni");
        }
    }
}

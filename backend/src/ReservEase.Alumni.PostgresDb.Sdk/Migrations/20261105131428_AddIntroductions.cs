using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddIntroductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowIntroductions",
                schema: "alumni",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "IntroductionMessages",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    IntroductionId = table.Column<string>(type: "text", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntroductionMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Introductions",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    RequesterId = table.Column<string>(type: "text", nullable: false),
                    RecipientId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedById = table.Column<string>(type: "text", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageSenderId = table.Column<string>(type: "text", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "text", nullable: true),
                    RequesterReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecipientReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Introductions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberBlocks",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    BlockerId = table.Column<string>(type: "text", nullable: false),
                    BlockedId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberBlocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntroductionMessages_InstitutionId",
                schema: "alumni",
                table: "IntroductionMessages",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntroductionMessages_IntroductionId_CreatedAt",
                schema: "alumni",
                table: "IntroductionMessages",
                columns: new[] { "IntroductionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_InstitutionId",
                schema: "alumni",
                table: "Introductions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_RecipientId",
                schema: "alumni",
                table: "Introductions",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_RequesterId",
                schema: "alumni",
                table: "Introductions",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Introductions_Status",
                schema: "alumni",
                table: "Introductions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBlocks_BlockedId",
                schema: "alumni",
                table: "MemberBlocks",
                column: "BlockedId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBlocks_BlockerId_BlockedId",
                schema: "alumni",
                table: "MemberBlocks",
                columns: new[] { "BlockerId", "BlockedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberBlocks_InstitutionId",
                schema: "alumni",
                table: "MemberBlocks",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntroductionMessages",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "Introductions",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MemberBlocks",
                schema: "alumni");

            migrationBuilder.DropColumn(
                name: "AllowIntroductions",
                schema: "alumni",
                table: "Members");
        }
    }
}

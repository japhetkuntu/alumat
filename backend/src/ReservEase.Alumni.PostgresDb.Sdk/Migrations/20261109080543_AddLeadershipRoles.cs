using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadershipRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadershipPosts",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ScopeType = table.Column<string>(type: "text", nullable: false),
                    ScopeId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeadershipRoles",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Privileges = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipPosts_InstitutionId",
                schema: "alumni",
                table: "LeadershipPosts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipPosts_InstitutionId_ScopeType_ScopeId",
                schema: "alumni",
                table: "LeadershipPosts",
                columns: new[] { "InstitutionId", "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipPosts_MemberId",
                schema: "alumni",
                table: "LeadershipPosts",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipPosts_RoleId",
                schema: "alumni",
                table: "LeadershipPosts",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipRoles_InstitutionId",
                schema: "alumni",
                table: "LeadershipRoles",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadershipPosts",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "LeadershipRoles",
                schema: "alumni");
        }
    }
}

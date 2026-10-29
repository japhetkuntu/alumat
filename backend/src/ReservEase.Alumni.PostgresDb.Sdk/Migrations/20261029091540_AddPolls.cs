using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PollAspirants",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    PollId = table.Column<string>(type: "text", nullable: false),
                    PositionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    MemberId = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollAspirants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PollParticipations",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    PollId = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollParticipations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PollPositions",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    PollId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollPositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Polls",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OpensAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    YearGroups = table.Column<List<int>>(type: "integer[]", nullable: true),
                    CommunityId = table.Column<string>(type: "text", nullable: true),
                    ResultsMode = table.Column<string>(type: "text", nullable: false),
                    ResultsPublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosingReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Polls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PollVotes",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InstitutionId = table.Column<string>(type: "text", nullable: false),
                    PollId = table.Column<string>(type: "text", nullable: false),
                    PositionId = table.Column<string>(type: "text", nullable: false),
                    AspirantId = table.Column<string>(type: "text", nullable: true),
                    Choice = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollVotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PollAspirants_InstitutionId",
                schema: "alumni",
                table: "PollAspirants",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollAspirants_PollId",
                schema: "alumni",
                table: "PollAspirants",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_PollAspirants_PositionId",
                schema: "alumni",
                table: "PollAspirants",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollParticipations_InstitutionId",
                schema: "alumni",
                table: "PollParticipations",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollParticipations_PollId_MemberId",
                schema: "alumni",
                table: "PollParticipations",
                columns: new[] { "PollId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollPositions_InstitutionId",
                schema: "alumni",
                table: "PollPositions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollPositions_PollId",
                schema: "alumni",
                table: "PollPositions",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_InstitutionId",
                schema: "alumni",
                table: "Polls",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_InstitutionId_Status_ClosesAt",
                schema: "alumni",
                table: "Polls",
                columns: new[] { "InstitutionId", "Status", "ClosesAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_InstitutionId",
                schema: "alumni",
                table: "PollVotes",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_PollId_PositionId",
                schema: "alumni",
                table: "PollVotes",
                columns: new[] { "PollId", "PositionId" });

            // Polls is new and off until platform staff switch it on, so every institution that exists now starts without it.
            migrationBuilder.Sql(@"UPDATE alumni.""Institutions"" SET ""DisabledFeatures"" = ""DisabledFeatures"" || '[""Polls""]'::jsonb WHERE NOT (""DisabledFeatures"" @> '[""Polls""]'::jsonb);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PollAspirants",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "PollParticipations",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "PollPositions",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "Polls",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "PollVotes",
                schema: "alumni");
        }
    }
}

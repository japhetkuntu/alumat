using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddPollIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_AspirantId",
                schema: "alumni",
                table: "PollVotes",
                column: "AspirantId");

            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_PositionId",
                schema: "alumni",
                table: "PollVotes",
                column: "PositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PollAspirants_PollPositions_PositionId",
                schema: "alumni",
                table: "PollAspirants",
                column: "PositionId",
                principalSchema: "alumni",
                principalTable: "PollPositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PollParticipations_Polls_PollId",
                schema: "alumni",
                table: "PollParticipations",
                column: "PollId",
                principalSchema: "alumni",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PollPositions_Polls_PollId",
                schema: "alumni",
                table: "PollPositions",
                column: "PollId",
                principalSchema: "alumni",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PollVotes_PollAspirants_AspirantId",
                schema: "alumni",
                table: "PollVotes",
                column: "AspirantId",
                principalSchema: "alumni",
                principalTable: "PollAspirants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PollVotes_PollPositions_PositionId",
                schema: "alumni",
                table: "PollVotes",
                column: "PositionId",
                principalSchema: "alumni",
                principalTable: "PollPositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PollVotes_Polls_PollId",
                schema: "alumni",
                table: "PollVotes",
                column: "PollId",
                principalSchema: "alumni",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollAspirants_PollPositions_PositionId",
                schema: "alumni",
                table: "PollAspirants");

            migrationBuilder.DropForeignKey(
                name: "FK_PollParticipations_Polls_PollId",
                schema: "alumni",
                table: "PollParticipations");

            migrationBuilder.DropForeignKey(
                name: "FK_PollPositions_Polls_PollId",
                schema: "alumni",
                table: "PollPositions");

            migrationBuilder.DropForeignKey(
                name: "FK_PollVotes_PollAspirants_AspirantId",
                schema: "alumni",
                table: "PollVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PollVotes_PollPositions_PositionId",
                schema: "alumni",
                table: "PollVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PollVotes_Polls_PollId",
                schema: "alumni",
                table: "PollVotes");

            migrationBuilder.DropIndex(
                name: "IX_PollVotes_AspirantId",
                schema: "alumni",
                table: "PollVotes");

            migrationBuilder.DropIndex(
                name: "IX_PollVotes_PositionId",
                schema: "alumni",
                table: "PollVotes");
        }
    }
}

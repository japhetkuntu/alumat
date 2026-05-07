using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umat.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotJsonbColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventRsvps_EventSnapshot_EventId",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropForeignKey(
                name: "FK_EventRsvps_MemberSnapshot_MemberId",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropTable(
                name: "EventSnapshot",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "MemberSnapshot",
                schema: "alumni");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='alumni' AND table_name='PaymentTransactions' AND column_name='Campaign') THEN
    ALTER TABLE alumni.""PaymentTransactions"" ADD COLUMN ""Campaign"" jsonb;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='alumni' AND table_name='PaymentTransactions' AND column_name='Member') THEN
    ALTER TABLE alumni.""PaymentTransactions"" ADD COLUMN ""Member"" jsonb;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='alumni' AND table_name='EventRsvps' AND column_name='Event') THEN
    ALTER TABLE alumni.""EventRsvps"" ADD COLUMN ""Event"" jsonb;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='alumni' AND table_name='EventRsvps' AND column_name='Member') THEN
    ALTER TABLE alumni.""EventRsvps"" ADD COLUMN ""Member"" jsonb;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='alumni' AND table_name='Contributions' AND column_name='Campaign') THEN
    ALTER TABLE alumni.""Contributions"" ADD COLUMN ""Campaign"" jsonb;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='alumni' AND table_name='Contributions' AND column_name='Member') THEN
    ALTER TABLE alumni.""Contributions"" ADD COLUMN ""Member"" jsonb;
  END IF;
END
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Campaign",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Member",
                schema: "alumni",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Event",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "Member",
                schema: "alumni",
                table: "EventRsvps");

            migrationBuilder.DropColumn(
                name: "Campaign",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "Member",
                schema: "alumni",
                table: "Contributions");

            migrationBuilder.CreateTable(
                name: "EventSnapshot",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Venue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberSnapshot",
                schema: "alumni",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    ProfilePictureUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberSnapshot", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_EventRsvps_EventSnapshot_EventId",
                schema: "alumni",
                table: "EventRsvps",
                column: "EventId",
                principalSchema: "alumni",
                principalTable: "EventSnapshot",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventRsvps_MemberSnapshot_MemberId",
                schema: "alumni",
                table: "EventRsvps",
                column: "MemberId",
                principalSchema: "alumni",
                principalTable: "MemberSnapshot",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

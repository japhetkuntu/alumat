using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberPhoneIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNormalized",
                schema: "alumni",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneVerifiedAt",
                schema: "alumni",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_InstitutionId_PhoneNormalized_Verified",
                schema: "alumni",
                table: "Members",
                columns: new[] { "InstitutionId", "PhoneNormalized" },
                unique: true,
                filter: "\"PhoneVerifiedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Members_PhoneNormalized",
                schema: "alumni",
                table: "Members",
                column: "PhoneNormalized");

            // Backfill: the same rules as PhoneNumbers.Normalize (digits only; 00 prefix dropped; a local number with a leading 0,
            // or the 9 national digits, gets Ghana's 233; kept only if 8 to 15 digits). Nobody is verified by this, only normalised.
            migrationBuilder.Sql(@"
UPDATE alumni.""Members"" AS m
SET ""PhoneNormalized"" = r.n
FROM (
  SELECT ""Id"", CASE WHEN length(x) BETWEEN 8 AND 15 THEN x ELSE NULL END AS n
  FROM (
    SELECT ""Id"",
      CASE
        WHEN d = '' THEN ''
        WHEN NOT p AND d LIKE '00%' THEN substr(d, 3)
        WHEN NOT p AND d LIKE '0%' AND length(d) BETWEEN 9 AND 11 THEN '233' || substr(d, 2)
        WHEN NOT p AND d NOT LIKE '233%' AND length(d) = 9 THEN '233' || d
        ELSE d
      END AS x
    FROM (
      SELECT ""Id"", regexp_replace(""Phone"", '[^0-9]', '', 'g') AS d, ltrim(""Phone"") LIKE '+%' AS p
      FROM alumni.""Members"" WHERE ""Phone"" IS NOT NULL
    ) a
  ) b
) r
WHERE m.""Id"" = r.""Id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_InstitutionId_PhoneNormalized_Verified",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Members_PhoneNormalized",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PhoneNormalized",
                schema: "alumni",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAt",
                schema: "alumni",
                table: "Members");
        }
    }
}

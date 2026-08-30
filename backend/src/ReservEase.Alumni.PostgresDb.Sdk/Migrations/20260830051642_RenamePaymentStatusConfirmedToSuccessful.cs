using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservEase.Alumni.PostgresDb.Sdk.Migrations
{
    /// <inheritdoc />
    public partial class RenamePaymentStatusConfirmedToSuccessful : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE alumni.""Contributions"" SET ""Status""='Successful' WHERE ""Status""='Confirmed';");
            migrationBuilder.Sql(@"UPDATE alumni.""PaymentTransactions"" SET ""Status""='Successful' WHERE ""Status""='Confirmed';");
            migrationBuilder.Sql(@"UPDATE alumni.""StoreOrders"" SET ""Status""='Successful' WHERE ""Status""='Confirmed';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE alumni.""Contributions"" SET ""Status""='Confirmed' WHERE ""Status""='Successful';");
            migrationBuilder.Sql(@"UPDATE alumni.""PaymentTransactions"" SET ""Status""='Confirmed' WHERE ""Status""='Successful';");
            migrationBuilder.Sql(@"UPDATE alumni.""StoreOrders"" SET ""Status""='Confirmed' WHERE ""Status""='Successful';");
        }
    }
}
